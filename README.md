Unity-Console
===============

Unity-Console is a Debug Console hook for Unity

==========================================================================
1. What it does
==========================================================================
  
  The libraries involve will enable opening a console which allows
    user to run commands for inspecting global state of the Unity
    application.
    
  It is incredibly limited and originally envisioned as an IronPython
    console but the author had difficulty getting a version of IPy
    running under the version of mono Unity uses.
    
  Instead there are some basic commands for inspecting global state
    typically of the Assembly-CSharp libraries.
    
==========================================================================
2. How it works
==========================================================================

  The basic idea is to run custom code in unity that enables opening a 
    console window that is under the control of this library.
    
  This is done with the winmm.dll which is placed in same directory as
    the Unity application.  This dll will forward standard winmm calls
    to the real winmm library but will use the timeGetTime function to 
    allow itself to run periodically and check for key presses.
    
  When the Console+Key is pressed or is automatically loaded by the timer
    then the winmm proxy will load the Unity.Console assembly which 
    will do the work of enabling some commands.
    
==========================================================================
3. Directory Structure Overview
==========================================================================

  \config         INI file used to 
  
  \Unity.Console  Library for doing basic shell scripting for unity
  
  \winmm          Hook for attaching to mono and loading the Unity.Console
  
  \Winmm.Test     Testing library which can be loaded into Unity and
                  used to verify functionality

                  
==========================================================================
4. Examples
==========================================================================
    
    >>> help
    help  - Display this help
    call  - Call Object Static method
    get   - Get field or property value
    set   - Set field or property value
    list  - List Properties and Methods of class
    clear - Clear screen
    ^Z    - Close Console (Control-Z + Enter)
    
    >>> list
      InnerStruct
      MMInternalClass
      MMPublicClass
      
    >>> list mmInternalClass
    call MMInternalClass Method
    get  MMInternalClass Field               Int32
    get  MMInternalClass <Property>k__BackingField   Int32
    get  MMInternalClass Array               InnerStruct[]
    get  MMInternalClass Property            Int32
    
    >>> call mmInternalClass method
    Result:
    42
    
    >>> get mmInternalClass array
    Result:
         id               name             value
    [ 0] 0                first            11
    [ 1] 1                second           12
    [ 2] 2                third            13
    [ 3] 3                fourth           14
    [ 4] 4                fifth            15
    
    >>> let $a = MMInternalClass Array 1
    Result:
    InnerStruct
    strProp : "second"
    id      : 1
    name    : "second"
    value   : 12

    >>> let $b = $a value
    Result:
    12
    >>> let $b = $a name
    Result:
    "second"
    >>> let $c = MMInternalClass Array
    Result:
         id               name             value
    [ 0] 0                first            11
    [ 1] 1                second           12
    [ 2] 2                third            13
    [ 3] 3                fourth           14
    [ 4] 4                fifth            15

    >>> let $b = $c 0
    Result:
    InnerStruct
    strProp : "first"
    id      : 0
    name    : "first"
    value   : 11

    >>> set $b value 1234
    Result:
    1234
    >>> get $b
    Result:
    InnerStruct
    strProp : "first"
    id      : 0
    name    : "first"
    value   : 1234
    
    >>> ; 
    >>> let $e = MMInternalClass Array 1..2
    >>> get $e
    Result:
         id               name             value
    [ 0] 1                second           12
    [ 1] 2                third            13

    >>> let $f = $e 0..0
    >>> get $f
    Result:
         id               name             value
    [ 0] 1                second           12

    >>> let $f = $e 1..1
    >>> get $f
    Result:
         id               name             value
    [ 0] 2                third            13

    >>> get $f 0
    Result:
    InnerStruct
    strProp : "third"
    id      : 2
    name    : "third"
    value   : 13

==========================================================================
5. TODO
==========================================================================
  
  There is way too much to do like handling array and objects better.
  
  In the future I'd rather just get IronPython running than create a 
    hacky one-off scripting language like this.
    
  I did try to use csharp from the mono project but Unity projects 
    frequently use internal and private classes which are hard to 
    use in that environment.  Maybe I'll expose what I had in the future.
  
==========================================================================
6. Contact
==========================================================================

  The code is offered as-is for demonstration purposes.
  
  The author apologizes for not having time to maintain the code but
    hopes that it is still useful to other users.

  The official homepage:
    https://github.com/TheHologram/unity-console
  
