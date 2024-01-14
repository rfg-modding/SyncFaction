
-- This script demonstrates how to teleport the player via lua

rsl.Log("Player position pre-teleport: {}", Player.Position:GetDataString()) -- All vectors support GetDataString for convenience
rfg.TeleportPlayer(rfg.Vector:new(1434.0, 15.0, 693.3)) --Teleport to Oasis safehouse
rsl.Log("Player position post-teleport: {}", Player.Position:GetDataString()) 