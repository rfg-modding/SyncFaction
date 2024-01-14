
--[[ Note that since this script is called main.lua (case doesn't matter), 
 it's run on script loader startup, right after the 3 activation beeps.
 Once event driven functions are in this will be the way to go for having
 a mod that runs when the game starts, where you'll setup all your event hooks
 For now this is mainly useful for setting up console commands, like what this 
 example does. By having a function defined here the function becomes available 
 for use by the lua state at RSL startup, meaning you can use it in the console
 as it's simply a lua interpreter.]]

-- Takes an object name or handle and reports back on if an object 
-- with that name/handle exists and what it's position is.
function ObjectExists(TargetName)

    TargetObject = rfg.GetObject(TargetName)

    if TargetObject == nil then
        rsl.Log("Target object of name/handle \"{}\" not found\n", TargetName)
    else
        rsl.Log("Target object of name/handle \"{}\" found\n", TargetName)
        rsl.Log("Position: {}\n", TargetObject.Position:GetDataString())
    end
end

-- Destroys all humans ;) (kills them).
function DestroyAllHumans()
    for i=0, World.Objects:Size(), 1 do -- Iterate through the world object list
        Object = World.Objects[i] -- Alias the object at index i for convenience
        
        if Object.Type == rfg.ObjectTypes.Human then -- Filter each object by checking if they are human objects 
            if Object.AllIndex ~= Player.AllIndex then --Don't want to kill the player
                Human = Object:CastToHuman() -- By default objects on the object lists are just objects, once you know they're a human, 
                                             -- cast them to be human objects to access human specific variables.
                Human.HitPoints = 0 --Kill them by setting their hit points = 0
                --rsl.Log("Killed human at index: {}", i)
            end
        end
    end
end

-- Teleports all humans except the player to where the player is pointing.
function PartyTime()
    for i=0, World.Objects:Size(), 1 do -- Iterate through the world object list
        Object = World.Objects[i] -- Alias the object at index i for convenience
        
        if Object.Type == rfg.ObjectTypes.Human then -- Filter each object by checking if they are human objects 
            if Object.AllIndex ~= Player.AllIndex then --Don't want to kill the player
                Human = Object:CastToHuman() -- By default objects on the object lists are just objects, once you know they're a human, 
                                             -- cast them to be human objects to access human specific variables.
                NewPosition = rfg.Vector:new(Player.AimPos)
                NewPosition.y = NewPosition.y + 5
                rfg.TeleportHuman(Human, NewPosition)
            end
        end
    end
end
