@echo off
rem ILU Prompter on a single screen: the output opens as a 960 x 540 window instead of
rem fullscreen on a second display. For trying the app out; not for a show.
call "%~dp0run_app.bat" --dev-output-on-primary %*
