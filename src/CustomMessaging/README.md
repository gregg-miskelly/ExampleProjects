# Custom Messaging

This is a quick sample on using the custom messaging APIs to send messages between
components. Custom messaging is used when an extension needs to run code in multiple
contexts, and needs to communicate information between them. For example, if an extension
has a UI component and a backend component, and needs to communicate between them.

This example uses a UI component (a Visual Studio package -- SampleUI) and debugger backend component.

Custom messaging can also be used to communicate between the msvsmon worker process and the
main Visual Studio process, between the IDE and remote debugger, and lots of other cases.

To use the sample:
1. Open the solution in Visual Studio
2. Set 'SampleUI' as the startup project
3. Hit F5 to launch it
4. When the debugged Visual Studio launches, open a "Hello World" project and start debugging
5. From the menu select Debug->Custom Message Test
6. Click the button