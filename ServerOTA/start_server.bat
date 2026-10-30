@echo off
title Monopoly Bank OTA Server
cd /d "%~dp0"
echo Starting Monopoly Bank OTA Server on port 3000...
node server.js
pause
