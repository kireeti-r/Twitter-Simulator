**DOSP PROJECT 4 PART 2** 

**Group Members:** 

KRISHNA KIREETI RAYAPROLU - 6303 1300 

Srisharanya Injarapu - 7595 6698 

**Instructions for running the code:** 

dotnet fsi server\_2.fsx 

dotnet fsi client.fsx 10 30 

dotnet fsi client.fsx (Number of users to be simulated, Duration between login and logout in seconds). **Demo Video Link:** 

[https://youtu.be/M73OWMPDtXQ** ](https://youtu.be/M73OWMPDtXQ)

**Project Explanation:** 

**Server Side:** 

Has all the end points such as Register User, Login, Logout, Tweet etc 

The server and the client communicate with each other using web sockets. These **web sockets** are implemented using  **Suave library**. 

The user has to register first in order to use all the functionalities of twitter. 

When someone tweets, it is saved to user’s tweet table and follower’s tweet table. The tweet is sent to all the followers of that particular user. 

Along with that, all the hashtags and mentions in the tweet are also saved in hashtag table and mentions table. 

We have endpoints such as HashtagQ, MentionsQ and GetAllUserTweets. 

The data from all these endpoints come to Suave webserver. 

When the request comes to the Suave server, it sends the data to the backend of actors. 

**Client Side:** 

The client.fsx file is run after the server is started on different port in different process in terminal 2, Client boots at IP: 0.0.0.0 and port 8080 

Client emulates the whole operation of twitter. 

The User has to register and then login with a valid username and password. ZipFDistribution function prepares distribution of subscribers and tweets. 

A Zipf distribution of followers is constructed for a given number of users, with the user with the highest rank having more followers than the user with the lowest rank. A user with a high rank tweets more, and the tweets are also distributed using ZipF distribution. 

**Occurrence = (ZipF constant \* Total Number of Elements) / (Rank of the Element).** Here ZipF constant is considered to be 0.30 for both tweet and subscriber distribution. 

We simulate the users with the help of actors where each actor acts as a user. 

**Flow of actions performed by the user:** 

1) The user first registers on twitter app 
1) User logs in after successful registration 
1) User can enjoy functionalities like subscribe, tweet, retweet etc 
1) User session is the time elapsed between login and logout 

**Maximum Load:** 

Maximum number of users simulated: 700, Session time 250 seconds 

**Performance Stats:** 



|**Users** |**Session Time** |**Registration Requests** |**Login Requests** |**Follow Requests** |**Tweet/Retweet  Requests** |**Logout Requests** |**Data Query Requests** |
| - | :- | :- | :- | :- | :- | :- | :- |
|**20** |**60** |**15** |**15** |**7** |**110** |**15** |**13** |
|**100** |**120** |**80** |**80** |**66** |**375** |**79** |**70** |
|**300** |**180** |**250** |**250** |**180** |**1200** |**250** |**210** |
|**500** |**240** |**300** |**300** |**225** |**2330** |**293** |**265** |
|**700** |**250** |**445** |**445** |**270** |**3150** |**435** |**405** |



|**Users** |**Session Time** |**Successful Registration Requests** |**Successful Login Requests** |**Successful Follow Requests** |**Successful Tweet/Retweet  Requests** |**Successful Logout Requests** |**Successful Data Query Requests** |
| - | :- | :- | :- | :- | :- | :- | :- |
|**20** |**60** |**15** |**15** |**6** |**100** |**15** |**13** |
|**100** |**120** |**80** |**80** |**59** |**320** |**80** |**69** |
|**300** |**180** |**250** |**250** |**143** |**1125** |**245** |**190** |
|**500** |**240** |**300** |**291** |**198** |**1900** |**270** |**225** |
|**700** |**250** |**445** |**423** |**201** |**2650** |**410** |**398** |


