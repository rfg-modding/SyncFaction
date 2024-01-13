
-- Made by moneyl. Originally uploaded to nexus mods. 
-- See the nexus mods page for installation and usage instructions.
-- Requires the RSL to work. See it's installation guide here: https://rsl.readthedocs.io/en/latest/Installation.html

Tele = {}

local function Distance3D(vector1, vector2)
    x = (vector2.x - vector1.x) ^ 2
    y = (vector2.y - vector1.y) ^ 2
    z = (vector2.z - vector1.z) ^ 2
    return math.sqrt(x + y + z)
end

if(rsl.BuildDate == nil) or (rsl.BuildDate < 20191224) then
    rfg.RegisterEvent("Initialized", 
    function()
        rfg.AddMessageBox(rfg.MessageBoxTypes.Ok, "Telekinesis Mod", "Your RSL is out of date! You'll need to update it to use Tele mod.")
    end, "[Telekinesis] Out of date init event")
    return
end

local function UiUpdateEvent_PerFrame(EventData)
    --Tele.UiHandle = rfg.ChangeUserMessage(Tele.UiHandle, "Current havok handle: " .. ToString(Tele.HavokHandle))
    Tele.UiHandleImpulse = rfg.ChangeUserMessage(Tele.UiHandleImpulse, "Telekinesis strength: " .. ToString(Tele.MaxForce))
    
    if(Tele.TeleActive) then
        if(Tele.TargetObject ~= nil) then
            NormalizedAimDir = rfg.GetLookDirection()
            AimLine = NormalizedAimDir:Scale(Tele.TeleDistance)
            NewTargetPos = rfg.Vector:new(Player.Position + AimLine + Tele.TargetOffset)
            TargetMoveLine = NewTargetPos - Tele.TargetObject.Position
            NormalizedTargetMoveLine = TargetMoveLine:UnitVector()

            ImpulseScale = Tele.MaxForce --* EventData.Frametime

            rfg.HavokBodyApplyLinearImpulse(Tele.HavokHandle, NormalizedTargetMoveLine:Scale(ImpulseScale))

            AppliedImpulse = NormalizedTargetMoveLine:Scale(ImpulseScale)            

            --rfg.ChangeUserMessage(Tele.UiHandleTargetPos, "Target pos: " .. NewTargetPos:ToString())
            rfg.ChangeUserMessage(Tele.UiHandleTargetMass, "Target mass: " .. ToString(rfg.HavokBodyGetMass(Tele.HavokHandle)))
            --rfg.ChangeUserMessage(Tele.UiHandleAppliedImpulse, "Applied impulse magnitude: " .. ToString(AppliedImpulse:Magnitude()))
        else
            --rsl.Log("Deactivating telekinesis because Tele.TargetObject is nil\n")            
            Tele.TeleActive = false
        end
    end
end

-- For now always returns true until a more reliable way of checking this can be found
local function IsObjectMoveable(Object)
    --[[if(Object.Type == rfg.ObjectTypes.Mover) then
        if(Object.SubType == rfg.ObjectSubTypes.MoverRfg) then
            return false
        end
    end--]]
    return true
end

local function KeypressEvent(EventData)
    if(EventData.KeyDown) then
        if(EventData.ControlDown) then
            if(EventData.KeyCode == rfg.KeyCodes.q) then
                Tele.MaxForce = Tele.MaxForce - 100
            elseif(EventData.KeyCode == rfg.KeyCodes.e) then
                Tele.MaxForce = Tele.MaxForce + 100
            end
        else
            if(EventData.KeyCode == rfg.KeyCodes.t) then

                if(Tele.TeleActive) then
                    --rsl.Log("Deactivating telekinesis from keybind\n")
                    rfg.ObjectiveHighlightRemove(Tele.TargetObject.Handle)
                    Tele.TeleActive = false
                else
                    NewTarget = rfg.GetObject(Player.AimTarget)
                    if(NewTarget ~= nil) then
                        if(IsObjectMoveable(NewTarget)) then
                            if(NewTarget.HavokHandle ~= 4294967295) then
                                Tele.TargetObject = NewTarget
                                Tele.HavokHandle = NewTarget.HavokHandle
                                Tele.TeleDistance = Distance3D(Player.Position, NewTarget.Position)
                                --rsl.Log("Tele.TeleDistance: {}\n", Tele.TeleDistance)
                                Tele.TeleActive = true
                                --rsl.Log("Activating telekinesis from keybind\n")

                                rfg.HavokBodyForceActivate(Tele.HavokHandle)
                                rfg.ObjectiveHighlightAdd(Tele.TargetObject.Handle, 1, 1)
                            end
                        end
                    end
                end
    
            elseif(EventData.KeyCode == rfg.KeyCodes.q) then
                Tele.MaxForce = Tele.MaxForce - 10
            elseif(EventData.KeyCode == rfg.KeyCodes.e) then
                Tele.MaxForce = Tele.MaxForce + 10
            elseif(EventData.KeyCode == rfg.KeyCodes.f) then
                NormalizedAimDir = rfg.GetLookDirection()
                rfg.ObjectiveHighlightRemove(Tele.TargetObject.Handle)
                rfg.HavokBodyApplyLinearImpulse(Tele.HavokHandle, NormalizedAimDir:Scale(Tele.ThrowForce))
                Tele.TeleActive = false
            end
        end
    end
end

local function MouseEvent(EventData)
    if(EventData.Scrolled) then
        Tele.TeleDistance = Tele.TeleDistance + (EventData.ScrollDelta / 120.0)
    end
end

local function OnLoad()
    if(Tele.Initialized) then --Only call after first init to avoid repeatedly registering events
        --Remove and re-add the user messages since they often disappear when reloading.
        --rfg.RemoveUserMessage(Tele.UiHandle)
        rfg.RemoveUserMessage(Tele.UiHandleImpulse)
        --rfg.RemoveUserMessage(Tele.UiHandleTargetPos)
        rfg.RemoveUserMessage(Tele.UiHandleTargetMass)
        --rfg.RemoveUserMessage(Tele.UiHandleAppliedImpulse)

        --Tele.UiHandle = rfg.AddUserMessage("Current havok handle: " .. ToString(Tele.HavokHandle), 0.02, 0.15, false, -1.0, rfg.MessageTypes.Other)
        Tele.UiHandleImpulse = rfg.AddUserMessage("Telekinesis strength: " .. ToString(Tele.MaxForce), 0.02, 0.15, false, -1.0, rfg.MessageTypes.Other)
        --Tele.UiHandleTargetPos = rfg.AddUserMessage("Target pos: ", 0.02, 0.25, false, -1.0, rfg.MessageTypes.Other)
        Tele.UiHandleTargetMass = rfg.AddUserMessage("Target mass: ", 0.02, 0.2, false, -1.0, rfg.MessageTypes.Other)
        --Tele.UiHandleAppliedImpulse = rfg.AddUserMessage("Applied impulse magnitude: ", 0.02, 0.35, false, -1.0, rfg.MessageTypes.Other)

        Tele.TeleActive = false
    end
end

local function Initialized()
    Tele.Initialized = true
    Tele.TeleActive = false
    Tele.TeleDistance = 10.0
    Tele.TargetOffset = rfg.Vector:new(0.0, 2.0, 0.0)
    Tele.TargetObject = nil
    Tele.HavokHandle = 0
    Tele.MaxForce = 1500
    Tele.ThrowForce = Tele.MaxForce * 2000
    --Tele.UiHandle = rfg.AddUserMessage("Current havok handle: " .. ToString(Tele.HavokHandle), 0.02, 0.15, false, -1.0, rfg.MessageTypes.Other)
    Tele.UiHandleImpulse = rfg.AddUserMessage("Telekinesis strength: " .. ToString(Tele.MaxForce), 0.02, 0.15, false, -1.0, rfg.MessageTypes.Other)
    --Tele.UiHandleTargetPos = rfg.AddUserMessage("Target pos: ", 0.02, 0.25, false, -1.0, rfg.MessageTypes.Other)
    Tele.UiHandleTargetMass = rfg.AddUserMessage("Target mass: ", 0.02, 0.2, false, -1.0, rfg.MessageTypes.Other)
    --Tele.UiHandleAppliedImpulse = rfg.AddUserMessage("Applied impulse magnitude: ", 0.02, 0.35, false, -1.0, rfg.MessageTypes.Other)
    rsl.Log("[Telekinesis mod] Tele.UiHandle: {}\n", Tele.UiHandle)

    rfg.RegisterEvent("Mouse", MouseEvent, "[Telekinesis] Mouse event")
    rfg.RegisterEvent("FrameUpdate", UiUpdateEvent_PerFrame, "[Telekinesis] Ui update event")
    rfg.RegisterEvent("Keypress", KeypressEvent, "[Telekinesis] Keypress event")
    rfg.RegisterEvent("Load", OnLoad, "[Telekinesis] Save load event")
end

Tele.Initialized = false
rfg.RegisterEvent("Initialized", Initialized, "[Telekinesis] Init event")