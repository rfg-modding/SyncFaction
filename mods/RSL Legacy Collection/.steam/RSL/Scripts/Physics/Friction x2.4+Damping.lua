-- FrictionPhysics v.2.0 
-- Made for Moneyl's RSL only!
-- Script by: V4XI5

-- Script start
    rfg.PhysicsSolver.Tau = 0.600
    rfg.PhysicsSolver.Damping = 0.780
    rfg.PhysicsSolver.FrictionTau = 2.200
    rfg.PhysicsSolver.DampDivTau = 1.600
    rfg.PhysicsSolver.TauDivDamp = 0.560
    rfg.PhysicsSolver.DampDivFrictionTau = 2.903
    rfg.PhysicsSolver.FrictionTauDivDamp = 0.100
    rfg.PhysicsSolver.ContactRestingVelocity = 0.900

rsl.LogWarning("FrictionMod Activated!x2.4 + Damping \n")

rfg.AddUiMessage("PHYSICS: Friction x2.4+Damp (Harder buildings with Buoyancy)", 3, true)
