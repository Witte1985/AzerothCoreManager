#!/usr/bin/env dotnet-script
#r "AzerothCoreManager.Infrastructure/bin/Debug/net10.0/AzerothCoreManager.Infrastructure.dll"
#r "AzerothCoreManager.Core/bin/Debug/net10.0/AzerothCoreManager.Core.dll"

using AzerothCoreManager.Infrastructure.Services.Parsers;
using System.IO;

var confContent = File.ReadAllText("/home/witte/.copilot/session-state/bb995f6a-1d4b-4381-ab24-8df4196745b1/files/paste-1777145279659.txt");
var parser = new PlayerbotConfigParser();
var sections = await parser.ParseAsync(confContent);

Console.WriteLine($"Found {sections.Count()} sections:");
foreach (var section in sections)
{
    Console.WriteLine($"\n  [{section.Name}] - {section.Options.Count()} options");
    foreach (var opt in section.Options.Take(2))
    {
        Console.WriteLine($"    - {opt.EnvVarName} = {opt.DefaultValue} ({opt.Type})");
    }
    if (section.Options.Count() > 2)
        Console.WriteLine($"    ... and {section.Options.Count() - 2} more");
}
