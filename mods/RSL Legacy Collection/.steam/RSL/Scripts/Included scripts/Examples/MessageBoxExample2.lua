
--This example shows how to use message boxes that expect user input 
--such as yes/no or accept/cancel buttons.
--See RSL/Core/rfg/MessageBoxTypes.lua for the other message box types

--This function is called when the user selects on of the message box options
local function MyMboxCallback(CallbackData)
    if(CallbackData.Choice == rfg.MessageBoxChoices.Yes) then
        rsl.Log("Selected \"Yes\" option in the message box\n")
    elseif(CallbackData.Choice == rfg.MessageBoxChoices.No) then
        rsl.Log("Selected \"No\" option in the message box\n")
    end
end

--Create the message box, register it's callback function as the last arg
rfg.AddMessageBox(rfg.MessageBoxTypes.YesNo, "Test message box", "Player pos: " .. Player.Position:ToString(), MyMboxCallback)