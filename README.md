Unity-Console
===============

Unity-Console is an IronPython-base Debug Console for Unity

==========================================================================
1. What it does
==========================================================================
  
  The libraries involved will enable opening a console which allows
    user to run commands for inspecting global state of the Unity
    application.
    
  It can be used to manipulate the game assets with some research
        
==========================================================================
2. How it works
==========================================================================

  The basic idea is to run custom code in unity that enables opening a 
    console window that is under the control of this library.

  Unity is not thread safe which means its not safe to perform actions
    on Unity elements from the Console without additional actions.  This
    is easier said than done but I have provided some support via the  
    coroutine library.
    
  The library will allow python code to be run in unity in small pieces
    this allows python to yield to for Unity to perform actions like 
    redrawing the view and doing other actions that it needs.
    
==========================================================================
3. Directory Structure Overview
==========================================================================

  \Plugins\Unity.Console.Plugin.dll
      Illusion Plug-In Architecture (IPA) Plugin for loading 
      the console functionality.  There are other ways but 
      this is best one available currently.
      IPA is written by EusthEnoptEron
      
      The current versions can be downloaded from here:
        https://github.com/Eusth/IPA/releases
  
  \Plugins\Console\
      Folder for most of the plugin.  Unity.Console.Dll is hard
      coded into the plugin above for launching.
  
  \Plugins\Console\Lib\
      This folder is the location for python based scripts.
      
             
==========================================================================
4. Installation
==========================================================================

    Prerequisites:
      1. Download IPA for Unity games
         The current versions can be downloaded from here:
          https://github.com/Eusth/IPA/releases
          
      2. Unpack IPA to game folder so IPA.exe is in same folder as app.
      
      3. Drag Game .EXE over IPA.EXE to instrument it.  This alters parts
         of the game in the managed folder to allow the plugins. It makes
         backups of files it changes but expect you to manually undo to 
         uninstall.
         
      4. Test game still works before adding console

    Install:
      1. Download the correct version of UnityConsole for your Unity 
         engine.  I dont know easy way to tell this.
        
      2. Unpack to game folder.  
      
      3. Edit Plugins\Console\Console.INI to your liking
      
      4. Add scripts Plugins\Console\lib\ for your game
             
==========================================================================
5. Examples
==========================================================================
    
    >>> import UnityEngine
    >>> print UnityEngine.Application.unityVersion
    5.3.5f1

    
    # the following decorator will wrap the capture function so 
    #   it can be safely called from python
    
    def unity(func):
        """ Decorator for wrapping unity calls """
        import functools
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            try:
                import coroutine
                if args == None: args=()
                return coroutine.start_new_coroutine(func, args, kwargs)
            except Exception:
                pass
        return wrapper

    @unity
    def capture():
        import UnityEngine
        UnityEngine.Application.CaptureScreenshot("\test.png");
        
==========================================================================
6. TODO
==========================================================================
   
  I'm sure there can be a lot done to improve some basic scripting libraries.
  
  Examples could be greatly expanded upon.
  
  Fix the issue with console not working correctly after being shutdown 
  the first time on subsequent reopening.
  
==========================================================================
7. Contact
==========================================================================

  The code is offered as-is for demonstration purposes.
  
  The author apologizes for not having time to maintain the code but
    hopes that it is still useful to other users.

  The official homepage:
    https://github.com/TheHologram/unity-console
  
