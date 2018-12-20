$exclude = @("*log*", "*bak*", "*png*", "*gif*", "*css*", "*jpg*")
$files = Get-ChildItem -Path "Help\*" -Recurse -exclude $exclude 

foreach ($file in $files){

$find = 'BRhodium.Bitcoin.Features.'
$replace = ''
    if ( -not $file.PSIsContainer)
        {
$content = Get-Content $($file.FullName)
#write replaced content back to the file
$content -replace $find,$replace | Out-File $($file.FullName) -encoding utf8
        }
		

}

Copy-Item -Path "icons\*" -Destination "Help\icons\" -Recurse -force
Copy-Item -Path "scripts\*" -Destination "Help\scripts\" -Recurse -force
Copy-Item -Path "styles\*" -Destination "Help\styles\" -Recurse -force