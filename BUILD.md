Unity-Console  Build 
====================

This document briefly describes steps to build the project

   
==========================================================================
1. Directory Structure Overview
==========================================================================

  \Library        Unity Mono related reference assemblies 
  
  \vendor         IronPython related reference assemblies
                  (specifically the Unity branch in my local ironpython
                   https://github.com/TheHologram/IronLanguages)
  
  \Unity.Console  The IronPython Console implementation with specific
                  fixes I needed for loading IronPython
  
  \Unity.Console.Plugin
                  Illusion Plug-In Architecture (IPA) Plugin for loading 
                  the console functionality.  There are other ways but 
                  this is best one available currently.
                  IPA is written by EusthEnoptEron
                  
  \Unity.Python.Modules
                  IronPython C# module that implements a coroutine-like
                  library used in scripts to avoid threading issues in 
                  unity

==========================================================================
2. Signing keys
==========================================================================
  In order to create the IronPython Unity module I needed access to 
  internals of IronPython.  I just altered my local IronPython build
  to allow this but needs the Unity.Python.Modules to be signed with known
  key.  I just used the IronPython keys out of lazyness but you need to 
  copy them to build.
  
  Copy Key.snk and DevelKey.snk from IronLanguanges\main repository to 
  root folder before building.
  
  I didn't feel good about checking in someone elses signing keys even
  though they are publically available.

  
==========================================================================
3. Libraries
==========================================================================
  I made a custom build of IronPython not only for the modules above but
  also needed it compiled against the specific libraries of unity to 
  ensure that they work correctly.
  
  Included is the Standard Python Library compiled to an Assembly as StdLib
    To build requires an old version of mono.  I dont recall which version 
    but probably used something around mono 2.8.1.
    
  To do the compile I used this as reference:
     https://stackoverflow.com/questions/24064327/compiling-ironpython-into-an-exe-that-uses-standard-library-packages
    
  Note that you need to use Mono with ipy to compile so that correct 
   mscorlib and System and System.Core are referenced.
   
    mono.exe --runtime=v2.0.50727 ipy.exe compilestdlib.py
  
==========================================================================
4. TODO
==========================================================================
  
  Probably way too much to document here at the moment.
  
==========================================================================
5. Contact
==========================================================================

  The code is offered as-is for demonstration purposes.
  
  The author apologizes for not having time to maintain the code but
    hopes that it is still useful to other users.

  The official homepage:
    https://github.com/TheHologram/unity-console
  
