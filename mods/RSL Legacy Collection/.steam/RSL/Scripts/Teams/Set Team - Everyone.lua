-- Set all humans to team
-- Available teams: Civilian, Guerrilla, EDF

Team = rfg.HumanTeams.EDF -- Change to the team of your choice.

    for i=0, World.Objects:Size(), 1 do
        Object = World.Objects[i]
        
        if Object.Type == rfg.ObjectTypes.Human then
            if Object.AllIndex ~= Player.AllIndex then
                Human = Object:CastToHuman()
                Human.CurrentTeam = Team
                rfg.AddUiMessage("TEAMS: All NPCs around joined EDF", 3, true)
                rsl.Log("Set teams! (only affects currently spawned NPC's)")
            end
        end
end







