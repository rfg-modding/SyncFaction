local function AAAIntro()
    rfg.AddUiMessage ("Intro: Welcome to Script Loader\nPress F1 to begin\nAlso, disable other overlay apps!", 5, true)
end

local function OnLoad()
    AAAIntro()
end
local function Initialized()
    AAAIntro()
end

rfg.RegisterEvent("Initialized", Initialized, "[AAAIntro]")
rfg.RegisterEvent("Load", OnLoad, "[AAAIntro]")







