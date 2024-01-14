
local function MyKeyEvent(EventData)
    if(EventData.KeyDown) then
        if(EventData.ControlDown) then
            rsl.Log("Control key down!\n")
            if(EventData.KeyCode == rfg.KeyCodes.Numpad7) then
                rsl.Log("Numpad7 pressed! Gravity.y = 20.0\n")
                rfg.SetGravity(0.0, 20.0, 0.0) --Shoots things towards the sky
            elseif(EventData.KeyCode == rfg.KeyCodes.Numpad8) then
                rsl.Log("Numpad8 pressed! Gravity.y = -9.8\n")
                rfg.SetGravity(0.0, -9.8, 0.0) --Vanilla game gravity
            elseif(EventData.KeyCode == rfg.KeyCodes.Numpad9) then
                rsl.Log("Numpad9 pressed! Gravity.x = 10.0\n") --
                rfg.SetGravity(10.0, 0.0, 0.0) --Accelerates things laterally instead of vertically
            end
        end
    end
end

rfg.RegisterEvent("Keypress", MyKeyEvent, "Gravity control keybinds")

