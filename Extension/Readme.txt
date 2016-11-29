
RestrictOperations_ParallelApproval adds functionality to lifecycle changes. The code is based on the Vault SDK sample RestrictOperations.
Additional functionality: allow 4, 6, 8... eyes approval to be run parallel, the order does not matter. Once all approval paths are released the final release state automatically activates. Extend existing lifecycles by configuration settings. For detailed information and PowerPoint slidedeck reach out to Markus Koechl; Autodesk VARs have access to the documentation in ChannelHub->Events->OTX2016-3_PDM section.
2017-11-29, Markus Koechl


------------------- original reamde -----------------
Overview:
RestrictOperations is a simple Vault extension which illustrates how to restrict operations by responding to Web Service Command Events.  The sample contains two parts.  The RestrictOperations.dll, which hooks to the Vault framework, and Configuratior.exe, which allows you to configure which operations are restricted.
 
RestrictOperation contains the following features/concepts:
	- Web Service Command Events
	- Restrictions


To Use:
Open RestrictOperations.sln in Visual Studio.  The project should open and compile with no errors.

Deploy the built files from the Configurator project to %programData%\Autodesk\Vault 2016\Extensions\RestrictOperations
NOTE: The %programData% variable may not be set for all operating systems.  In Windows XP and 2003, this is usually "C:\Documents and Settings\All Users\Application Data".  In Vista and above, this is usually "C:\ProgramData"

Run Configuratior.exe.  Check the boxes of the operations you want to restrict.  Launch any Vault client application on that computer and run the restricted command.  You should see the operation blocked in the client.
When boxes are checked or unchecked in Configurator, the result is immediate.  There is no need to restart the Vault client application.


Known issues:
- There is almost no error handling code.  
