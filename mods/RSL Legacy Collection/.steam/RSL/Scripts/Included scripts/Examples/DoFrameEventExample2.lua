
local Handle = rfg.AddUserMessage("Player.Position: " .. Player.Position:ToString(), 0.05, 0.1, false, -1.0, rfg.MessageTypes.Other)
local function MyEvent(EventData)
    rfg.ChangeUserMessage(Handle, "Player.Position: " .. Player.Position:ToString())
end

rfg.RegisterEvent("FrameUpdate", MyEvent)