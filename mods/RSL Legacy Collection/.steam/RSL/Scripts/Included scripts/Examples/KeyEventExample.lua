
--This function is called every time it's event is triggered
local function MyEvent(EventData)
    if EventData.KeyUp then
        rsl.Log("{} released!\n", EventData.KeyCode)
    end
end

--Register the function to be called every time
--a keypress event is triggered
rfg.RegisterEvent("Keypress", MyEvent)




