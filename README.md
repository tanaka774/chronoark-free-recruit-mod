# a few points of developing chronoark native mod

*this explanation is just for my case. It's possible this doesn't apply to other one.

## some dependencies and version

.net version: in .csproj, `<TargetFramework>net40</TargetFramework>`  
and build with `dotnet publish --configuration Release --framework net40`  

you can develop with BepInEx https://thunderstore.io/c/chrono-ark/p/BepInEx/BepInExPack_Chrono_Ark/  
but if you upload or use with in-game workshop you need to adapt this game's native mod style.  
and here is an explanation about the native mod style.  

download harmony manually from here https://github.com/pardeike/Harmony/releases and use `net472/0Harmony.dll`  
and, when using Harmony 2.4.2 with Chrono Ark native mods, use in code:  
`harmony.UnpatchAll(harmony.Id);`  
Not `UnpatchSelf()` (doesn't exist) and not parameterless `UnpatchAll()` (obsolete).  

## file structures

(I developed in linux(cachyos) but this should be similar to windows.)

```
(at Steam/steamapps/common/Chrono Ark/ChronoArk_Data/StreamingAssets/Mod/FreeRecruitMod/)
FreeRecruitMod/
├── Assemblies/
│   ├── FreeRecruitMod.dll (your compiled mod)
│   └── 0Harmony.dll (Harmony 2.4.2 net472 version)
└── ChronoArkMod.json (metadata)
└── cover.jpg (preview art)
```

if they're correct, you could see your mod in in-game workshop mode.
