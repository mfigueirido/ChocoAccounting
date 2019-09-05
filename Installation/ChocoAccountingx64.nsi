!define APPNAME "Choco Accounting"
!define COMPANYNAME "Enxebre Coding Group"
!define DESCRIPTION "Ultra simple and fast home accounting"
!define VERSIONMAJOR 1
!define VERSIONMINOR 0
!define VERSIONBUILD 1
!define HELPURL "https://chocoaccounting.wordpress.com/"
!define UPDATEURL "https://chocoaccounting.wordpress.com/"
!define ABOUTURL "https://chocoaccounting.wordpress.com/"
!define INSTALLSIZE 6950

RequestExecutionLevel admin

InstallDir "$PROGRAMFILES\${COMPANYNAME}\${APPNAME}"

LicenseData "gpl-3.0.rtf"

Name "${COMPANYNAME} - ${APPNAME}"
Icon "AppIcon.ico"
OutFile "ca-${VERSIONMAJOR}.${VERSIONMINOR}-installer.exe"

!include LogicLib.nsh

Page license
Page directory
Page instfiles

!macro VerifyUserIsAdmin
UserInfo::GetAccountType
pop $0
${If} $0 != "admin" ;Require admin rights
        MessageBox mb_iconstop "Administrator rights required!"
        SetErrorLevel 740 ;ERROR_ELEVATION_REQUIRED
        Quit
${EndIf}
!macroend

Function .onInit
	SetShellVarContext all
	!insertmacro VerifyUserIsAdmin
FunctionEnd

Section "install"

	InitPluginsDir
	File /oname=$PLUGINSDIR\SqlLocalDB.msi "SqlLocalDB.msi"
	IfFileExists "$PROGRAMFILES64\Microsoft SQL Server\120\LocalDB\Binn\sqlservr.exe" LocalDBExists LocalDBNotExists
	Goto LocalDBExists
	LocalDBNotExists:
	ExecWait '"msiexec" /i "$PLUGINSDIR\SqlLocalDB.msi"' $0
	Goto LocalDBExists
	LocalDBExists:
	
	SetOutPath $INSTDIR\en
	File "en\ChocoAccounting.resources.dll"
	
	SetOutPath $INSTDIR\es
	File "es\ChocoAccounting.resources.dll"
	
	SetOutPath $INSTDIR\gl
	File "gl\ChocoAccounting.resources.dll"
	
	SetOutPath $INSTDIR
	File "ChocoAccounting.exe"
	File "ChocoAccounting.exe.config"
	File "EntityFramework.dll"
	File "EntityFramework.SqlServer.dll"
	File "gpl-3.0.rtf"
	File "AppIcon.ico"
	
	WriteUninstaller "$INSTDIR\uninstall.exe"
	
	CreateDirectory "$SMPROGRAMS\${COMPANYNAME}"
	CreateShortCut "$SMPROGRAMS\${COMPANYNAME}\${APPNAME}.lnk" "$INSTDIR\ChocoAccounting.exe" "" "$INSTDIR\AppIcon.ico"
	
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "DisplayName" "${APPNAME}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "QuietUninstallString" "$\"$INSTDIR\uninstall.exe$\" /S"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "InstallLocation" "$\"$INSTDIR$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "DisplayIcon" "$\"$INSTDIR\AppIcon.ico$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "Publisher" "${COMPANYNAME}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "HelpLink" "$\"${HELPURL}$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "URLUpdateInfo" "$\"${UPDATEURL}$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "URLInfoAbout" "$\"${ABOUTURL}$\""
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "DisplayVersion" "${VERSIONMAJOR}.${VERSIONMINOR}.${VERSIONBUILD}"
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "VersionMajor" ${VERSIONMAJOR}
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "VersionMinor" ${VERSIONMINOR}
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "NoModify" 1
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "NoRepair" 1
	WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}" "EstimatedSize" ${INSTALLSIZE}
	
SectionEnd

Function un.onInit
	SetShellVarContext all
	MessageBox MB_OKCANCEL "Permanantly remove ${APPNAME}?" IDOK Next
		Abort
	Next:
	!insertmacro VerifyUserIsAdmin
FunctionEnd

Section "uninstall"

	Delete "$SMPROGRAMS\${COMPANYNAME}\${APPNAME}.lnk"
	
	RmDir "$SMPROGRAMS\${COMPANYNAME}"

	Delete $INSTDIR\en\ChocoAccounting.resources.dll
	Delete $INSTDIR\es\ChocoAccounting.resources.dll
	Delete $INSTDIR\gl\ChocoAccounting.resources.dll
	Delete $INSTDIR\ChocoAccounting.exe
	Delete $INSTDIR\ChocoAccounting.exe.config
	Delete $INSTDIR\EntityFramework.dll
	Delete $INSTDIR\EntityFramework.SqlServer.dll
	Delete $INSTDIR\gpl-3.0.rtf
	Delete $INSTDIR\AppIcon.ico
	
	Delete $INSTDIR\uninstall.exe
	
	RmDir $INSTDIR\en
	RmDir $INSTDIR\es
	RmDir $INSTDIR\gl
	RmDir $INSTDIR
	
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${COMPANYNAME} ${APPNAME}"
	
SectionEnd