# Models/Domain
Think of Models as blueprints for the data that we want to move around our
game. If the client and server need to talk to each other, they use the classes
defined in this folder. These classes should mostly just contain properties
(`get; set;`). Could also be Data Transfer Objects (DTOs) or Database Entities.

There should be no logic tho, Models are supposed to be pretty simple. Also no
dependency injection.