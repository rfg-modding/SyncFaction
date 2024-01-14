--------------------------------
--ULTIMATE GRAVITY MOD BY CAMO
--------------------------------
UpEarth = rfg.Vector:new(0.0, 9.80665, 0.0)
rfg.SetGravity(UpEarth)
rsl.Log("Reversed Gravity Set [Manual]\n")
rfg.AddUiMessage ("PHYSICS: Reversed", 3, true)
