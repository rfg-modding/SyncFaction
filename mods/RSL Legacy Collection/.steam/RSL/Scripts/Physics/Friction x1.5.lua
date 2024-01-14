-- FrictionPhysics v.2.0 
-- Made for Moneyl's RSL only!
-- Script by: V4XI5

-- Script start
    rfg.PhysicsSolver.Tau = 0.600
    rfg.PhysicsSolver.Damping = 0.490
    rfg.PhysicsSolver.FrictionTau = 1.800
    rfg.PhysicsSolver.DampDivTau = 1.097
    rfg.PhysicsSolver.TauDivDamp = 0.290
    rfg.PhysicsSolver.DampDivFrictionTau = 2.903

rsl.LogWarning("FrictionMod Activated!x1.5 \n")

rfg.AddUiMessage("PHYSICS: Friction x1.5 (Stiffer buildings)", 3, true)