
local Handle = rfg.AddUserMessage(string.format("%.2f", Player.Velocity:Magnitude() * 3.6) .. " kph", 0.05, 0.1, false, -1.0, rfg.MessageTypes.Other)
local function MyEvent(EventData)
    rfg.ChangeUserMessage(Handle, string.format("%.2f", Player.Velocity:Magnitude() * 3.6) .. " kph")
end

rfg.RegisterEvent("FrameUpdate", MyEvent)


