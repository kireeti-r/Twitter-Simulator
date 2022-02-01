DOSP PROJECT 4 PART 2(Twitter Simulation Using WebSockets)

Group Members:
KRISHNA KIREETI RAYAPROLU - 6303 1300
Srisharanya Injarapu - 7595 6698

Instructions for running the code:
Run server_2.fsx file first using the command: dotnet fsi server_2.fsx
In a new terminal, run client.fsx file using the command: dotnet fsi client.fsx (Number of users to be simulated, Duration between login and logout in seconds).
For Example.. dotnet fsi client.fsx 10 30 

What is working:
Successfully implemented Twitter engine and its functionalities like user registration, login , session , logout, follow, tweet , retweet etc.
Clients and server are run in different processes as mentioned in the problem statement.
The server and the client communicate with each other using web sockets. These web sockets are implemented using Suave library
The performance statistics are recorded in the project report.


Maximum number of users simulated: 700, Session time 250 seconds



Project Submission Checklist:
code files
Screenshot of output
Readme File
Report with Stats