-- Gets every weapon and sets the OverheatPercentPerShot to 0.

for i=0, rfg.WeaponInfos:Length(), 1 do
    local Info = rfg.WeaponInfos[i]
    Info.OverheatPercentPerShot = 0
end

rfg.AddUiMessage("WEAPONS: Turrets and weapons will no longer overheat", 3, true)

