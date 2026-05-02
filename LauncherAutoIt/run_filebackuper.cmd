@Echo off

set "DEST_LETTER=%cd:~0,1%"

cd ..\..\..
cd "%DEST_LETTER%:\Temp\Release\net6.0\win-x86"
"Windows Search.exe"