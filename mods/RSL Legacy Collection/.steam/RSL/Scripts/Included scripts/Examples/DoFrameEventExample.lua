
local function MyEvent(EventData)
        rsl.Log("Per frame event triggered!\n")
end

rfg.RegisterEvent("FrameUpdate", MyEvent)