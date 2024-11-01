This is MQTT protocol app designed to recieve any payload and handle it and send it to all that are subscribed to it. It will not spam subscribers with the same data unless change happened. Before use you must configure appsettings.json.
This app supports live comunication with databases between subscribers and publishers all you need to do is make your db and make a connection to publisher db in appsettings.json. If you want to publish something else to subscribers you need to make a file called
publishing and pack your data into that file. 
