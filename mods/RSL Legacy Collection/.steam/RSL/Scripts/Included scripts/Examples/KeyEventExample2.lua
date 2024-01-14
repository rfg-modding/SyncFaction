
local MyTimer = rsl.Timer:new(true)
local function MyEvent(EventData)
    if(EventData.KeyDown) then
        if(EventData.KeyCode == rfg.KeyCodes.Numpad7) then
            if(MyTimer:ElapsedMilliseconds() > 1000) then
                MyTimer:Reset()
                rsl.Log("Numpad 7 pressed! Enough time has passed since the last press!\n")                
            else
                rsl.Log("Not enough time has elapsed. Time elapsed: {}\n", MyTimer:ElapsedMilliseconds())
            end
        end
    end
end

rfg.RegisterEvent("Keypress", MyEvent)

