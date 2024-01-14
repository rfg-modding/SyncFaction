
-- Run this with the F4 console to remove labels
function RemoveLabels()
    for i = 0, 1000, 1
    do
        rfg.RemoveUserMessage(i)
    end
end

-- Remove labels from previous runs in case the script breaks and you need to run it multiple times (you can also call this function manually from the F4 console)
RemoveLabels()

-- Create labels
local speedLabel = rfg.AddUserMessage("", 0.05, 0.1, false, -1.0, rfg.MessageTypes.Other)
local nameLabel = rfg.AddUserMessage("", 0.05, 0.15, false, -1.0, rfg.MessageTypes.Other)
local displayNameLabel = rfg.AddUserMessage("", 0.05, 0.20, false, -1.0, rfg.MessageTypes.Other)
local engineTorqueLabel = rfg.AddUserMessage("", 0.05, 0.25, false, -1.0, rfg.MessageTypes.Other)
local massLabel = rfg.AddUserMessage("", 0.05, 0.30, false, -1.0, rfg.MessageTypes.Other)
local minRpmLabel = rfg.AddUserMessage("", 0.05, 0.35, false, -1.0, rfg.MessageTypes.Other)
local maxRpmLabel = rfg.AddUserMessage("", 0.05, 0.40, false, -1.0, rfg.MessageTypes.Other)
local optimalRpmLabel = rfg.AddUserMessage("", 0.05, 0.45, false, -1.0, rfg.MessageTypes.Other)

-- Run every frame
local function VehicleOverlay_FrameUpdate(EventData)
    playerVehicleHandle = Player.VehicleHandle
    vehicleObj = rfg.GetObject(playerVehicleHandle) -- Get player vehicle

    if vehicleObj ~= nil then
        vehicle = vehicleObj:CastToVehicle() -- Upcast vehicle from object to vehicle type so we can access vehicle specific types
        if vehicle ~= nil then 
            -- Update labels
            rfg.ChangeUserMessage(speedLabel, string.format("%.2f", vehicle.LastVelocity:Magnitude() * 3.6) .. " kph")
            rfg.ChangeUserMessage(nameLabel, "Name: " .. vehicle.Info.Name)
            rfg.ChangeUserMessage(displayNameLabel, "Display name: " .. vehicle.Info.DisplayName)
            rfg.ChangeUserMessage(engineTorqueLabel, "Engine torque: " .. string.format("%.2f", vehicle.Info.EngineTorque))
            rfg.ChangeUserMessage(massLabel, "Mass: " .. string.format("%.2f", vehicle.Info.ChassisMass))
            rfg.ChangeUserMessage(minRpmLabel, "Min RPM: " .. string.format("%.2f", vehicle.Info.MinRpm))
            rfg.ChangeUserMessage(maxRpmLabel, "Max RPM: " .. string.format("%.2f", vehicle.Info.MaxRpm))
            rfg.ChangeUserMessage(optimalRpmLabel, "Optimal RPM: " .. string.format("%.2f", vehicle.Info.OptimalRpm))
            return
        end
    end

    -- Couldn't get player vehicle. Empty out all labels but one noting that no vehicle was found
    rfg.ChangeUserMessage(speedLabel, "")
    rfg.ChangeUserMessage(nameLabel, "Not in a vehicle")
    rfg.ChangeUserMessage(displayNameLabel, "")
    rfg.ChangeUserMessage(engineTorqueLabel, "")
    rfg.ChangeUserMessage(massLabel, "")
    rfg.ChangeUserMessage(minRpmLabel, "")
    rfg.ChangeUserMessage(maxRpmLabel, "")
    rfg.ChangeUserMessage(optimalRpmLabel, "")
end

-- Tell RSL to run VehicleOverlay_FrameUpdate every frame. The third value is the event label used in the event viewer (under menu > scripts > event viewer, I think)
rfg.RegisterEvent("FrameUpdate", VehicleOverlay_FrameUpdate, "VehicleOverlayFrameUpdate")

rfg.AddUiMessage ("VEHICLES: Overlay enabled. Disable from console with RemoveLabels()", 10, true)
