
-- This script demonstrates the logging capabilities of the RSL

--[[ 
   Simple test of logging using variadic function arguments. 
   Using fmt (https://github.com/fmtlib/fmt) and so it supports multiple
   formatting styles. C# formatting shown below, python style formatting
   and printf style formatting.
   The benefit of this formatting is the you can have more complex outputs
   without needing ..  and tostring() all over the place. Only downside for
   now is that you need to remember to stick a newline (\n) for each log
   so it moves to the next line
  ]]
rsl.Log("f1: {}, f2: {}\n", 8, "bark") -- Supports value substitution/formatting. See the fmtlib docs for more examples of this
rsl.Log("{1} are {0}", "red", "apples") -- Supports reordering inputting values

-- Also supports logging with different tags like [Error] or [Warning]
rsl.LogNone("LogNone test\n") -- Notice that there's no tag to the left of the message in the logs.
rsl.LogInfo("LogInfo test\n") -- Really the same thing as rsl.Log
rsl.LogWarning("LogWarning test\n")
rsl.LogError("LogError test\n")
rsl.LogFatalError("LogFatalError test\n")
rsl.LogLua("LogLua test\n")
rsl.LogJson("LogJson test\n")

--[[
   If you check the logs folder after running these you'll notice that the output should be in some of those as well
   Log files are filtered out by tag type so you should see that certain tagged log messages won't be in 
   certain files.
  ]]