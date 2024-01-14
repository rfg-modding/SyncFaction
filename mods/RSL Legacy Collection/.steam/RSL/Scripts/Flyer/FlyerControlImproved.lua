
local this = {}
this.Activated = false
this.Flyer = nil
this.UiHandleEngineForce = rfg.AddUserMessage("", 0.02, 0.15, false, -1.0, rfg.MessageTypes.Swap)
this.HavokHandle = 0
this.MoveForce = 10
rsl.Log("this.UiHandleEngineForce: {}\n", this.UiHandleEngineForce)

local function FrameUpdateEvent(EventData)
    message = ""
    if(this.Flyer ~= nil) then
        if(this.Activated) then
            message = "FLYER force: " .. ToString(this.MoveForce)
            this.Flyer.ReqVel.x = 0
            this.Flyer.ReqVel.y = 0
            this.Flyer.ReqVel.z = 0
            this.Flyer.ReqPointIsValid = false
            this.Flyer.ReqStopAtPoint = false
        end
    end
    rfg.ChangeUserMessage(this.UiHandleEngineForce, message)
end

local function KeypressEvent(EventData)
    if(EventData.KeyDown) then
        if(this.Activated) then
            if(EventData.KeyCode == rfg.KeyCodes.v) then
                rsl.Log("Deactivating flyer control manually!\n")
                this.Activated = false
            elseif(EventData.KeyCode == rfg.KeyCodes.e) then
                rsl.Log("Deactivating flyer control because of vehicle exit!\n")
                this.Activated = false
            elseif(EventData.KeyCode == rfg.KeyCodes.w) then
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyLinearImpulse(this.HavokHandle, ForwardUnitVec:Scale(this.MoveForce))
            elseif(EventData.KeyCode == rfg.KeyCodes.s) then
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyLinearImpulse(this.HavokHandle, ForwardUnitVec:Scale(-1.0 * this.MoveForce))
            elseif(EventData.KeyCode == rfg.KeyCodes.a) then
                RightUnitVec = this.Flyer.Orientation.rvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, RightUnitVec:Scale(-15), this.Flyer.Position + ForwardUnitVec:Scale(100))
            elseif(EventData.KeyCode == rfg.KeyCodes.d) then
                RightUnitVec = this.Flyer.Orientation.rvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, RightUnitVec:Scale(15), this.Flyer.Position + ForwardUnitVec:Scale(100))
            elseif(EventData.KeyCode == rfg.KeyCodes["ArrowDown"]) then
                UpUnitVec = this.Flyer.Orientation.uvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, UpUnitVec:Scale(20), this.Flyer.Position + ForwardUnitVec:Scale(100))
            elseif(EventData.KeyCode == rfg.KeyCodes["ArrowUp"]) then
                UpUnitVec = this.Flyer.Orientation.uvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.fvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, UpUnitVec:Scale(-20), this.Flyer.Position + ForwardUnitVec:Scale(100))
            elseif(EventData.KeyCode == rfg.KeyCodes["ArrowLeft"]) then
                UpUnitVec = this.Flyer.Orientation.uvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.rvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, UpUnitVec:Scale(10), this.Flyer.Position + ForwardUnitVec:Scale(100))
            elseif(EventData.KeyCode == rfg.KeyCodes["ArrowRight"]) then
                UpUnitVec = this.Flyer.Orientation.uvec:UnitVector()
                ForwardUnitVec = this.Flyer.Orientation.rvec:UnitVector()
                rfg.HavokBodyApplyPointImpulse(this.HavokHandle, UpUnitVec:Scale(-10), this.Flyer.Position + ForwardUnitVec:Scale(100))
            
            elseif(EventData.ControlDown) then
                DownVector = rfg.Vector:new(0.0, -1.0, 0.0)
                rfg.HavokBodyApplyLinearImpulse(this.HavokHandle, DownVector:Scale(this.MoveForce))
            elseif(EventData.ShiftDown) then
                UpVector = rfg.Vector:new(0.0, 1.0, 0.0)
                rfg.HavokBodyApplyLinearImpulse(this.HavokHandle, UpVector:Scale(this.MoveForce))
            elseif(EventData.KeyCode == rfg.KeyCodes.z) then
                if(this.MoveForce > 0) then
                    this.MoveForce = this.MoveForce - 10
                end
            elseif(EventData.KeyCode == rfg.KeyCodes.x) then
                this.MoveForce = this.MoveForce + 10
            end
        else
            if(EventData.KeyCode == rfg.KeyCodes.v) then
                -- teleport player to a flyer
                NewTarget = rfg.GetObject(Player.AimTarget)
                if(NewTarget ~= nil) then
                    if(NewTarget.Type == rfg.ObjectTypes.Vehicle and NewTarget.SubType == rfg.ObjectSubTypes.VehicleFlyer) then
                        Veh = NewTarget:CastToVehicle()
                        if(Veh ~= nil) then
                            rfg.TeleportPlayerIntoVehicle(Veh)
                        end
                    end
                end
                -- enable flyer controls when player is inside
                Obj = rfg.GetObject(Player.VehicleHandle)
                if(Obj ~= nil) then
                    if(Obj.Type == rfg.ObjectTypes.Vehicle and Obj.SubType == rfg.ObjectSubTypes.VehicleFlyer) then
                        Flyer = Obj:CastToFlyer()
                        if(Flyer ~= nil) then
                            this.Flyer = Flyer
                            this.Activated = true
                            this.Flyer.EngineForce = 1
                            this.HavokHandle = Flyer.HavokHandle
                            this.MoveForce = 750
                            rsl.Log("Enabling flyer control!\nthis.Flyer.EngineForce: {}\nthis.Activated: {}\n, this.MoveForce: {}\n", this.Flyer.EngineForce,
                            this.Activated, this.MoveForce)
                        end
                    end
                end
            end
        end
    end
end

rfg.RegisterEvent("Keypress", KeypressEvent, "[FlyerControl] Keypress event")
rfg.RegisterEvent("FrameUpdate", FrameUpdateEvent, "[FlyerControl] Ui update event")


rfg.AddUiMessage ("FLYER: V enter, WASD move, ARROWS tilt, Shift/Ctrl height, X/Z force", 10, true)
