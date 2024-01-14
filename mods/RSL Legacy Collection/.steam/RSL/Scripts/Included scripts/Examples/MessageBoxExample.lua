
-- Simple example of how to use message boxes. This version is for simple message boxes that don't require
-- user input (such as ones with only an "OK" button). For message boxes that expect a yes/no response
-- see the second example file.
rfg.AddMessageBox(rfg.MessageBoxTypes.Ok, "Test message box", "Player pos: " .. Player.Position:ToString())
