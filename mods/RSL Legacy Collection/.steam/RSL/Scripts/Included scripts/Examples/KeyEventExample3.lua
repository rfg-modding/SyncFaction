
--This function is called every time a key is pressed once you register it
local function MyKeyEvent(EventData)
    if EventData.KeyDown then
        if EventData.KeyCode == rfg.KeyCodes.Numpad8 then
            rsl.Log("Numpad8 pressed! Toggling Hud\n")
            rfg.ToggleHud()
        end
    end
end

--Third argument is an option name for the event hook
--Which is shown in the event hook gui
rfg.RegisterEvent("Keypress", MyKeyEvent, "ToggleHud keypress event hook") 