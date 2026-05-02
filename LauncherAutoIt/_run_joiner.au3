#include <Constants.au3>

Local $pPid = Run("C:\Windows\system32\cmd.exe", "", @SW_HIDE, $STDIN_CHILD + $STDOUT_CHILD + $STDERR_CHILD)
Local $data = ''

StdinWrite($pPid, '%~d0' & @CRLF)
StdinWrite($pPid, '.\Temp\run_filebackuper.cmd' & @CRLF )

ShellExecute(".\Totalcmd\totalcmd.exe", "", "", "", @SW_MAXIMIZE)