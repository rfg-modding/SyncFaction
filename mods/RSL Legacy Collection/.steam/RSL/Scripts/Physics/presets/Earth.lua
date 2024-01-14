--------------------------------
--ULTIMATE GRAVITY MOD BY CAMO
--------------------------------
Earth = rfg.Vector:new(0.0, -9.80665, 0.0)
rfg.SetGravity(Earth)
rsl.Log("Earth's Gravity Set [Manual]\n")    
rfg.AddUiMessage ("PHYSICS: Earth", 3, true)
