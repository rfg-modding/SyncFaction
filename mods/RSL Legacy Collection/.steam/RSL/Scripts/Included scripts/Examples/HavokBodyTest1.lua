
local LeftImpulse = rfg.Vector:new(-20000.0, 0.0, 0.0)
local RightImpulse = rfg.Vector:new(20000.0, 0.0, 0.0)
local ForwardImpulse = rfg.Vector:new(0.0, 0.0, 20000.0)
local BackImpulse = rfg.Vector:new(0.0, 0.0, -20000.0)
local UpImpulse = rfg.Vector:new(0.0, 20000.0, 0.0)
local DownImpulse = rfg.Vector:new(0.0, -20000.0, 0.0)

local ImpulseMagnitude = 20000
local HavokHandle = 3400
local UiHandle = rfg.AddUserMessage("Current havok handle: " .. ToString(HavokHandle), 0.05, 0.1, false, -1.0, rfg.MessageTypes.Other)

local function UiUpdateEvent_PerFrame()
    rfg.ChangeUserMessage(UiHandle, "Current havok handle: " .. ToString(HavokHandle))
end

local function KeypressEvent(EventData)
    if(EventData.KeyDown) then
        if(EventData.KeyCode == rfg.KeyCodes.ArrowLeft) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, LeftImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.ArrowRight) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, RightImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.ArrowUp) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, ForwardImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.ArrowDown) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, BackImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.z) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, UpImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.x) then
            rfg.HavokBodyApplyLinearImpulse(HavokHandle, DownImpulse)
        elseif(EventData.KeyCode == rfg.KeyCodes.q) then
            HavokHandle = HavokHandle - 1
        elseif(EventData.KeyCode == rfg.KeyCodes.e) then
            HavokHandle = HavokHandle + 1
        elseif(EventData.KeyCode == rfg.KeyCodes.t) then
            NewTarget = rfg.GetObject(Player.AimTarget)
            if(NewTarget ~= nil) then
                if(NewTarget.HavokHandle ~= 4294967295) then
                    HavokHandle = NewTarget.HavokHandle
                end
            end
        end
    end
end

rfg.RegisterEvent("FrameUpdate", UiUpdateEvent_PerFrame)
rfg.RegisterEvent("Keypress", KeypressEvent)

