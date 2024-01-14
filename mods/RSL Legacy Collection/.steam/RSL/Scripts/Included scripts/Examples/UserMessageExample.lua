
--This example shows how to create user message that displays the characters position
--Which updates every time a key is pressed (per frame events will be better for this once they're in)

--Create the user message and save the handle for later use. Note that the lifespan is -1.0, so it lasts forever
--You can user rfg.RemoveUserMessage(Handle) to delete a message with infinite lifespan
local Handle = rfg.AddUserMessage("Player.Position: " .. Player.Position:ToString(), 0.05, 0.1, false, -1.0, rfg.MessageTypes.Other)
rsl.Log("Handle: {}\n", Handle)
local function MyKeyEvent(EventData)
    rfg.ChangeUserMessage(Handle, "Player.Position: " .. Player.Position:ToString())
end

rfg.RegisterEvent("Keypress", MyKeyEvent, "Data overlay update")

