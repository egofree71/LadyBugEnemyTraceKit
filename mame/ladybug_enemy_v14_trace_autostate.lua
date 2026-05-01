-- Lady Bug enemy movement trace logger for MAME Lua
-- --------------------------------------------------
-- Goal: capture an oracle trace from the original arcade program so a Godot
-- trace can be compared tick/frame by tick/frame.
--
-- Usage example:
--   mame ladybug -window -console -autoboot_script ladybug_enemy_trace.lua -autoboot_delay 1
--
-- v14-autostate variant: based on the older known-working callback style.
-- The script itself loads CONFIG.save_state, waits CONFIG.start_delay_frames, captures 900 samples, then exits MAME.
-- Do not also pass -state on the MAME command line for this script.

local CONFIG = {
    output_prefix = "ladybug_enemy_v14_trace", -- creates *_initial_snapshot.json and *_trace.jsonl
    output_dir = ".",                       -- use forward slashes on Windows, e.g. "C:/temp/ladybug-traces"
    frames_to_capture = 900,                -- around 15 seconds at 60 fps
    start_delay_frames = 30,                -- lets the Lua save-state load settle
    save_state = "test1",                  -- Lua loads this state after startup; use nil/"" to disable
    exit_when_done = true,                 -- true = ask MAME to exit after capture
    pause_when_done = false,                 -- true = pause after capture
    include_full_memory_each_frame = false  -- heavy; normally keep false
}

local cpu = nil
local mem = nil
local trace_file = nil
local snapshot_file = nil
local summary_file = nil
local initialized = false
local save_state_load_attempted = false
local global_frame = 0
local sample = 0
local snapshot_written = false
local done = false

local function h2(v)
    return string.format("%02X", v & 0xff)
end

local function h4(v)
    return string.format("%04X", v & 0xffff)
end

local function boolstr(v)
    return v and "true" or "false"
end

local function escape_json(s)
    s = tostring(s or "")
    s = s:gsub('\\', '\\\\')
    s = s:gsub('"', '\\"')
    s = s:gsub('\n', '\n')
    s = s:gsub('\r', '\\r')
    s = s:gsub('\t', '\\t')
    return s
end

local function q(s)
    return '"' .. escape_json(s) .. '"'
end

local function arr(items)
    return "[" .. table.concat(items, ",") .. "]"
end

local function obj(items)
    return "{" .. table.concat(items, ",") .. "}"
end

local function read_u8(addr)
    return mem:read_u8(addr) & 0xff
end

local function read_range_hex(start_addr, length)
    local out = {}
    for i = 0, length - 1 do
        out[#out + 1] = h2(read_u8(start_addr + i))
    end
    return table.concat(out, "")
end

local function state_value(names)
    if not cpu or not cpu.state then
        return nil
    end
    for _, name in ipairs(names) do
        local item = cpu.state[name]
        if item ~= nil then
            return item.value
        end
    end
    return nil
end

local function state_hex(names, digits)
    local v = state_value(names)
    if v == nil then
        return ""
    end
    if digits == 2 then return h2(v) end
    return h4(v)
end

local function enemy_slot_json(slot, base)
    local raw = read_u8(base)
    local dir = (raw >> 4) & 0x0f
    local collision_active = (raw & 0x02) ~= 0
    local bit0 = (raw & 0x01) ~= 0
    return obj({
        '"slot":' .. tostring(slot),
        '"addr":' .. q(h4(base)),
        '"raw":' .. q(h2(raw)),
        '"dir":' .. q(h2(dir)),
        '"bit0":' .. boolstr(bit0),
        '"collisionActive":' .. boolstr(collision_active),
        '"x":' .. q(h2(read_u8(base + 1))),
        '"y":' .. q(h2(read_u8(base + 2))),
        '"sprite":' .. q(h2(read_u8(base + 3))),
        '"attr":' .. q(h2(read_u8(base + 4)))
    })
end

local function enemies_json()
    return arr({
        enemy_slot_json(0, 0x602b),
        enemy_slot_json(1, 0x6030),
        enemy_slot_json(2, 0x6035),
        enemy_slot_json(3, 0x603a)
    })
end

local function player_json()
    return obj({
        '"raw":' .. q(h2(read_u8(0x6026))),
        '"x":' .. q(h2(read_u8(0x6027))),
        '"y":' .. q(h2(read_u8(0x6028))),
        '"sprite":' .. q(h2(read_u8(0x6029))),
        '"attr":' .. q(h2(read_u8(0x602a))),
        '"turnTargetX":' .. q(h2(read_u8(0x6196))),
        '"turnTargetY":' .. q(h2(read_u8(0x6197))),
        '"currentDir":' .. q(h2(read_u8(0x6198)))
    })
end

local function enemy_work_json()
    return obj({
        '"tempDir":' .. q(h2(read_u8(0x61bd))),
        '"tempX":' .. q(h2(read_u8(0x61be))),
        '"tempY":' .. q(h2(read_u8(0x61bf))),
        '"rejectedMask":' .. q(h2(read_u8(0x61c1))),
        '"fallbackMask":' .. q(h2(read_u8(0x61c2))),
        '"preferred":' .. arr({
            q(h2(read_u8(0x61c4))), q(h2(read_u8(0x61c5))),
            q(h2(read_u8(0x61c6))), q(h2(read_u8(0x61c7)))
        }),
        '"chaseTimers":' .. arr({
            q(h2(read_u8(0x61ce))), q(h2(read_u8(0x61cf))),
            q(h2(read_u8(0x61d0))), q(h2(read_u8(0x61d1)))
        }),
        '"chaseRoundRobin":' .. q(h2(read_u8(0x61d2)))
    })
end

local function timers_json()
    return obj({
        '"61B4":' .. q(h2(read_u8(0x61b4))),
        '"61B5":' .. q(h2(read_u8(0x61b5))),
        '"61B6":' .. q(h2(read_u8(0x61b6))),
        '"61B7":' .. q(h2(read_u8(0x61b7))),
        '"61B8":' .. q(h2(read_u8(0x61b8))),
        '"61B9":' .. q(h2(read_u8(0x61b9))),
        '"freeze61E1":' .. q(h2(read_u8(0x61e1))),
        '"collectibleColorCounter6199":' .. q(h4(read_u8(0x6199) + (read_u8(0x619a) << 8)))
    })
end

local function ports_json()
    return obj({
        '"in0_9000":' .. q(h2(read_u8(0x9000))),
        '"in1_9001":' .. q(h2(read_u8(0x9001))),
        '"dsw0_9002":' .. q(h2(read_u8(0x9002))),
        '"dsw1_9003":' .. q(h2(read_u8(0x9003)))
    })
end

local function door_tiles_json()
    -- 0x0D1D is a ROM table seen by the reverse engineering notes.  It appears
    -- to contain VRAM addresses related to the twenty rotating gates.
    -- This gives us a concrete snapshot even before all gate semantics are named.
    local items = {}
    for i = 0, 19 do
        local lo = read_u8(0x0d1d + i * 2)
        local hi = read_u8(0x0d1e + i * 2)
        local addr = lo + hi * 256
        local tile = ""
        local color = ""
        if addr >= 0xd000 and addr <= 0xd3ff then
            tile = h2(read_u8(addr))
            color = h2(read_u8(addr + 0x400))
        end
        items[#items + 1] = obj({
            '"index":' .. tostring(i),
            '"vramAddr":' .. q(h4(addr)),
            '"tile":' .. q(tile),
            '"color":' .. q(color)
        })
    end
    return arr(items)
end

local function frame_json()
    local fields = {
        '"schema":"ladybug.enemyTrace.v1"',
        '"sample":' .. tostring(sample),
        '"mameFrame":' .. tostring(global_frame),
        '"pc":' .. q(state_hex({"CURPC", "PC", "rPC"}, 4)),
        '"r":' .. q(state_hex({"R", "rR", "IR"}, 2)),
        '"player":' .. player_json(),
        '"enemies":' .. enemies_json(),
        '"enemyWork":' .. enemy_work_json(),
        '"timers":' .. timers_json(),
        '"ports":' .. ports_json()
    }

    if CONFIG.include_full_memory_each_frame then
        fields[#fields + 1] = '"ram6000_62AF":' .. q(read_range_hex(0x6000, 0x02b0))
        fields[#fields + 1] = '"logicalMaze6200_62AF":' .. q(read_range_hex(0x6200, 0x00b0))
    end

    return obj(fields)
end

local function snapshot_json()
    return obj({
        '"schema":"ladybug.enemyInitialSnapshot.v1"',
        '"mameVersion":' .. q(emu.app_name() .. " " .. emu.app_version()),
        '"romName":' .. q(emu.romname()),
        '"system":' .. q(emu.gamename()),
        '"saveStateRequested":' .. q(CONFIG.save_state),
        '"sampleStartMameFrame":' .. tostring(global_frame),
        '"pc":' .. q(state_hex({"CURPC", "PC", "rPC"}, 4)),
        '"r":' .. q(state_hex({"R", "rR", "IR"}, 2)),
        '"ram6000_62AF":' .. q(read_range_hex(0x6000, 0x02b0)),
        '"logicalMaze6200_62AF":' .. q(read_range_hex(0x6200, 0x00b0)),
        '"vramD000_D3FF":' .. q(read_range_hex(0xd000, 0x0400)),
        '"colorD400_D7FF":' .. q(read_range_hex(0xd400, 0x0400)),
        '"doorTilesFromRomTable0D1D":' .. door_tiles_json(),
        '"player":' .. player_json(),
        '"enemies":' .. enemies_json(),
        '"enemyWork":' .. enemy_work_json(),
        '"timers":' .. timers_json(),
        '"ports":' .. ports_json()
    })
end

local function output_path(suffix)
    local dir = CONFIG.output_dir or "."
    if dir == "" or dir == "." then
        return CONFIG.output_prefix .. suffix
    end

    local last = dir:sub(-1)
    if last == "/" or last == "\\" then
        return dir .. CONFIG.output_prefix .. suffix
    end

    return dir .. "/" .. CONFIG.output_prefix .. suffix
end

local function must_open(path, mode)
    local f, err = io.open(path, mode)
    if f == nil then
        error("Cannot open output file: " .. tostring(path) .. " / " .. tostring(err))
    end
    return f
end

local function open_outputs()
    trace_file = must_open(output_path("_trace.jsonl"), "w")
    snapshot_file = must_open(output_path("_initial_snapshot.json"), "w")
    summary_file = must_open(output_path("_summary.txt"), "w")
end

local function close_outputs()
    if trace_file ~= nil then trace_file:flush(); trace_file:close(); trace_file = nil end
    if snapshot_file ~= nil then snapshot_file:flush(); snapshot_file:close(); snapshot_file = nil end
    if summary_file ~= nil then summary_file:flush(); summary_file:close(); summary_file = nil end
end

local function finish_capture()
    if done then return end
    done = true
    if summary_file ~= nil then
        summary_file:write("Lady Bug enemy trace capture complete\n")
        summary_file:write("samples=" .. tostring(sample) .. "\n")
        summary_file:write("frames_to_capture=" .. tostring(CONFIG.frames_to_capture) .. "\n")
        summary_file:write("save_state=" .. tostring(CONFIG.save_state) .. "\n")
        summary_file:write("trace_file=" .. output_path("_trace.jsonl") .. "\n")
        summary_file:write("snapshot_file=" .. output_path("_initial_snapshot.json") .. "\n")
    end
    close_outputs()
    emu.print_info("Lady Bug enemy trace capture complete: " .. tostring(sample) .. " samples")
    manager.machine:popmessage("Lady Bug trace complete: " .. tostring(sample) .. " samples")
    if CONFIG.pause_when_done then
        pcall(function() emu.pause() end)
        pcall(function() manager.ui.single_step = true end)
    end
    if CONFIG.exit_when_done then
        manager.machine:exit()
    end
end

local function log_error(message)
    if emu ~= nil and emu.print_error ~= nil then
        pcall(function() emu.print_error(message) end)
    else
        print(message)
    end
end

local function initialize_if_needed()
    if initialized then return true end

    cpu = manager.machine.devices[":maincpu"]
    if cpu == nil then
        log_error("Lady Bug trace: cannot find :maincpu. Are you running the ladybug driver?")
        done = true
        return false
    end

    mem = cpu.spaces["program"]
    if mem == nil then
        log_error("Lady Bug trace: cannot find :maincpu program address space")
        done = true
        return false
    end

    local ok, err = pcall(open_outputs)
    if not ok then
        log_error("Lady Bug trace: " .. tostring(err))
        done = true
        return false
    end

    initialized = true

    if CONFIG.save_state ~= nil and CONFIG.save_state ~= "" and not save_state_load_attempted then
        save_state_load_attempted = true
        emu.print_info("Lady Bug trace: loading state " .. CONFIG.save_state)
        local load_ok, load_err = pcall(function() manager.machine:load(CONFIG.save_state) end)
        if not load_ok then
            log_error("Lady Bug trace: save-state load failed: " .. tostring(load_err))
            log_error("Lady Bug trace: continuing without loaded state; check CONFIG.save_state")
        end
    else
        emu.print_info("Lady Bug trace: no save state requested; capture starts after delay")
    end

    emu.print_info("Lady Bug trace: writing outputs with prefix " .. output_path(""))
    return true
end

local function on_start()
    -- With -autoboot_script, some MAME builds execute the script after machine start,
    -- so register_start may not fire for this run. on_frame_done calls this lazily too.
    initialize_if_needed()
end

local function on_frame_done()
    if done then return end
    if not initialize_if_needed() then return end
    global_frame = global_frame + 1

    if global_frame <= CONFIG.start_delay_frames then
        return
    end

    if not snapshot_written then
        snapshot_file:write(snapshot_json() .. "\n")
        snapshot_file:flush()
        snapshot_written = true
    end

    trace_file:write(frame_json() .. "\n")
    sample = sample + 1

    if sample % 60 == 0 then
        trace_file:flush()
        manager.machine:popmessage("Lady Bug trace: " .. tostring(sample) .. " / " .. tostring(CONFIG.frames_to_capture))
    end

    if sample >= CONFIG.frames_to_capture then
        finish_capture()
    end
end

emu.register_start(on_start)
emu.register_frame_done(on_frame_done, "ladybug_enemy_trace")
