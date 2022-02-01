open System.Net.WebSockets

#time "on"
#r "csProject.dll"

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 
#r "nuget: Akka.Remote"
#r "nuget: FSharp.Json" 
open FSharp.Json


open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Collections.Generic
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading


//Type declaration for matching cases within actor
type RegisterUser = int   
type StatusOfRegistration =  bool * string  // username passowrd

type LoginUser = int * int 
type StatusOfLogin = bool * bool * string
type LogoutUser = int * int * int 
type StatusOfLogout = bool * bool * bool * string
type UserSession = int * int * int * int 
type Subscription = int * int * int * int * int
type StatusOfSubscription = bool * bool * bool * bool * string
type Tweet = int * int * int * int * int * int
type StatusOfTweets = int * float * string
type TweetsFromSubscriptions = int * int * int * int * int * int * string * string * WebsocketService * ClientWebSocket
type RetweetsFromSubscriptions = int * int * int * int * int * int * int * string * string
type Retweet = float * string * string * WebsocketService * ClientWebSocket
type StatusOfRetweets = int * float * float * string
type HashtagQ = float * string
type MentionsQ =   float  * float * string
type GetUserTweetQ = float  * float * float * string
type HashTagQResponse = bool * string * string 
type MentionsQResponse = bool * bool * string * string 
type GetUserTweetsResponse = bool * bool * bool * string * string
type FollowOtherUsers = int * string * string   
type ServerResponse = string

type TweetReceieved = WebsocketService * ClientWebSocket * CancellationToken
type ReceiveFlag = bool



let mutable ShutDownExecution = false
let mutable Q1Flag : bool = false
let mutable Q2Flag : bool = false
let mutable Q3Flag : bool = false
let mutable ServerIdCount : int = 0
let mutable NumberOfUsers : int =20
let mutable NumberOfTweets : int = 100
let mutable SessionTimeFOrEachUserInSeconds : int = 60
let ServerInstances = 32

let args : string array = fsi.CommandLineArgs |>  Array.tail//
// //First argument is the total number of users to be simulated.
NumberOfUsers <- args.[0] |> int
SessionTimeFOrEachUserInSeconds <-  args.[1] |> int

let mutable NumberOfRequests : int = 0
let mutable RegistrationRequests : int = 0
let mutable PassedRegistrations : int = 0
let mutable LoginRequests : int = 0
let mutable PassedLoginRequests : int = 0
let mutable FollowRequests : int = 0
let mutable PassedFollowRequests : int = 0
let mutable TweetRequests : int = 0
let mutable PassedTweetRequests : int = 0
let mutable LogoutRequests: int = 0
let mutable PassedLogoutRequests: int = 0
let mutable DataQueryRequests : int = 0
let mutable PassedDataQueryRequests : int = 0


//This flag will turn on the stopwatch only once
let mutable LogoutWatchFlag = 1



let mutable SuccessfullyRegisteredUsers : int  = 0
let mutable SuccessfullyLoggedinUsers : int  = 0
let mutable SuccessfullyLoggedOutUsers : int  = 0
let mutable SuccessfullySubscribedUsers : int  = 0
let mutable SuccessfulTweets : int = 0

let hashTags = [| "#Gators"; "#Christmas"; "#ThanksGiving" ; "#UniversalStudios" ; "#UniversityOfFlorida" ; "#AlinDobra";"#F#" ; "#FunctionalProgramming" ; "#Metaverse" ; "#Celebs"|]
let mentions : List<string> = new List<string>() 

let mutable UsedHashtag : string = ""
let mutable UsedMention : string = ""

type RequestJsonObj= {
  RequestType : string;
  UserId : string;
  Password : string;
  TweetNumber : int
  Tweet : string;
  FollowUserId : string;
  SubscriptionUserName : string;
  Mention : string;
  HashTag : string
}

let mutable requestJsonDummy : RequestJsonObj = {RequestType = "none" ; UserId = "none" ; Password = "none" ; TweetNumber = 100; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}

type ResponseJsonObj= {
  RequestType : string;
  RequestStatus : string;
  ResponseString1 : string;
  ResponseString2 : string
  
}


let mutable DummyJsonResponse : ResponseJsonObj = {RequestType = "none" ;RequestStatus = "100" ; ResponseString1 = "none" ; ResponseString2 = "none" }

let getServerID() : int =
    if (ServerIdCount >= 
        ServerInstances) then
        ServerIdCount <- 0
    ServerIdCount <- ServerIdCount + 1
    ServerIdCount


let ShuffleArray (arrayArg : array<string>) = 
    for i in 0 .. (arrayArg.Length - 1 ) do
        let j = (System.Random()).Next(arrayArg.Length)
        let tempItem = arrayArg.[j]
        arrayArg.[j] <- arrayArg.[i]
        arrayArg.[i] <- tempItem
        printfn "3"


let mutable ZipFDistributedSubscriptionMap : Dictionary<string, List<string>> = new Dictionary<string, List<string>>() 
let mutable ZipFDistributedTweetMap : Dictionary<string, int> = new Dictionary<string, int>()
let prepareZipFDistributionForTweetsAndSubscribers(totalNumberofUsers : int , totalNumberOfTweets : int) =   
    let mutable userIdArray : string array = Array.zeroCreate NumberOfUsers
    printfn "1"
    for counter in 1 .. NumberOfUsers do
        let mutable userId : string = "user" + string(counter)
        userIdArray.[counter - 1] <- userId 
        let mutable subscriptionList = new List<string>()
        ZipFDistributedSubscriptionMap.Add(userId,subscriptionList)
        printfn "list added : %i" counter
    ShuffleArray(userIdArray)
    printfn "length of user Id array : %i " userIdArray.Length
    for counter2 in 1 .. NumberOfUsers do
        
        let mutable rankOfUser : int = counter2 
        
        let zipFConstant = 0.3 
        let mutable userId : string = "user" + string(counter2)

         
        
        let mutable numberOfSubscribersFollowingUser : int = Convert.ToInt32(Math.Floor( (zipFConstant * (float NumberOfUsers) ) / (float rankOfUser) ) )
       
        
        
        let mutable numberOfTweetsByUser : int = Convert.ToInt32(Math.Ceiling( (zipFConstant * (float NumberOfTweets) ) / (float rankOfUser) ) )
        
        ZipFDistributedTweetMap.Add(userId , numberOfTweetsByUser)
        printfn "4"
        
        let mutable i : int = 1
        
        printfn "numberOfSubscribersFollowingUser : %i" numberOfSubscribersFollowingUser
        printfn "numberOfTweetsByUser : %i" numberOfTweetsByUser

        let mutable loopcounter = 1
        let mutable whileFlag = true
        while i <= numberOfSubscribersFollowingUser && whileFlag do 
            loopcounter <- loopcounter + 1
            if(userIdArray.[i-1] <> userId)  then
               let mutable subscriptionListOfUser : List<string> = ZipFDistributedSubscriptionMap.Item(userIdArray.[i-1])
               subscriptionListOfUser.Add(userId)
               i <- i + 1
               printfn "5"
            if (loopcounter = userIdArray.Length) then
                whileFlag <- false


prepareZipFDistributionForTweetsAndSubscribers(NumberOfUsers , NumberOfTweets)

let system = ActorSystem.Create("ActorSystemUser")

type UserActor(usernameArg : string , passwordArg : string , sessionTimeArg : int , subscriptionCountArg : int , susbcriberListArg : List<string> , numberOfTweetsArg : int , tweetRateperSecondArg : int) =
  inherit Actor()
  let mutable username : string = usernameArg
  let mutable password : string = passwordArg
  let mutable SessionTime : int = sessionTimeArg
  let mutable SubscriptionCounter : int = subscriptionCountArg
  let mutable ListOfSubscriptions : List<string> = susbcriberListArg
  let mutable numberOfTweets : int = numberOfTweetsArg
  let mutable TweetsperSecond : int = tweetRateperSecondArg
  let mutable SuccessfulSubscriptions : int = 0
  let mutable numberOfTweets : int = numberOfTweetsArg
  let mutable RegistrationSuccessfulFlag : bool = false
  let mutable LoginSuccessfulFlag : bool = false
  let mutable LogoutSuccessfulFlag : bool = false
  let mutable SubscriptionSuccessfulFlag : bool = false
  let mutable LogOutFlag : bool = false

  
 
  let mutable cancellationTokenSource = new CancellationTokenSource(new TimeSpan(1, 1, 0, 0));
  let mutable tokenVal = cancellationTokenSource.Token;

  let mutable WebSocketApiURL : string = "ws://127.0.0.1:8080/websocket";
  let mutable WebSocketClient = WebsocketService()
  let mutable SocketObject = new ClientWebSocket()
   
  override x.OnReceive message =
     match message with
     | :? RegisterUser as msg -> let mutable requestJson : RequestJsonObj = {RequestType = "RegisterUser" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                                 let data : string = Json.serialize requestJson
                                             
                                 RegistrationRequests <- RegistrationRequests + 1
                                 WebSocketClient.Connect(SocketObject, WebSocketApiURL, tokenVal).Wait(tokenVal)
                                 WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                                 let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                                 printfn "responseJsonString : %s" responseJsonString
                                 let mutable responseJson : ResponseJsonObj = Unchecked.defaultof<_>
                                 try
                                 responseJson <- Json.deserialize<ResponseJsonObj>(responseJsonString)
                                 printf ""
                                 with
                                 | _ -> printfn "Registration ResponseJson Deserialization Failed"
                                 if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "RegisterUser" && responseJson.RequestStatus = "SUCCESS") then
                                                PassedRegistrations <- PassedRegistrations + 1
                                                x.Self <! (true,(responseJson.ResponseString1))
                                 else
                                                x.Self <! (false,("registration request failed for user :  " + username))

                                             
                                             

     | :? StatusOfRegistration as msg -> let mutable (userRegisterationStatus , serverMessage) = unbox<StatusOfRegistration> msg    
                                         if (userRegisterationStatus) then
                                             RegistrationSuccessfulFlag <- true 
                                             printfn "Server Response : %s " serverMessage
                                             x.Self <! (1,1)
                                         else printfn "Server Response : %s " serverMessage
                                         
     | :? LoginUser as msg -> if (RegistrationSuccessfulFlag) then
                                  let mutable requestJson : RequestJsonObj = {RequestType = "Login" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none"; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                                  let data : string = Json.serialize requestJson
                                  LoginRequests <- LoginRequests + 1
                                  WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                                  let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                                  let mutable responseJson  =  Unchecked.defaultof<_>
                                  try
                                  responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                  with 
                                  | _ -> printfn "Login ResponseJson Deserialization Failed"
                                  if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Login" && responseJson.RequestStatus = "SUCCESS") then
                                    PassedLoginRequests <- PassedLoginRequests + 1
                                    x.Self <! (true,true,(responseJson.ResponseString1))
                                  else
                                    x.Self <! (false,false,("Login Failed for User : " + username))

                              else
                              printfn "User with userId : %s cannot login as registration was unsuccessful" username
                   
     | :? StatusOfLogin as msg -> let mutable (userLoginStatus ,arg2, serverMessage) = unbox<StatusOfLogin> msg    
                                  if (userLoginStatus) then
                                    LoginSuccessfulFlag <- true 
                                    printfn "Server Response : %s " serverMessage
                                    // start subscription
                                    x.Self <! (1,1,1,1,1)
                                    // start session
                                    //x.Self <! (1,1,1,1)
                                  else      
                                    printfn "Server Response : %s " serverMessage    


     | :? Subscription as msg -> if (SubscriptionCounter = 0) then
                                     FollowRequests <- FollowRequests + 1
                                   // start session
                                     x.Self <! (1,1,1,1)
                                 else
                                     for i in 0 .. (ListOfSubscriptions.Count - 1) do       
                                        let mutable requestJson : RequestJsonObj = {RequestType = "Follow" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = ListOfSubscriptions.Item(i) ; SubscriptionUserName = "none" ; Mention = "none"; HashTag = "none"}
                                        let data : string = Json.serialize requestJson
                                        WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                                        let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                                        let mutable responseJson  =  Unchecked.defaultof<_>
                                        try
                                              responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                        with 
                                              | _ -> printfn "Subscription request ResponseJson Deserialization Failed"
                                        if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Follow" && responseJson.RequestStatus = "SUCCESS") then
                                               PassedFollowRequests <- PassedFollowRequests + 1
                                               x.Self <! (true,true,true,true,(responseJson.ResponseString1))
                                        else
                                               x.Self <! (false,false,false,false,("Follow Reuqest is done by "+username + " to follow " + ListOfSubscriptions.Item(i) + " failed"))                         
                          
     | :? UserSession as msg -> let mutable SessionLogoutTime : DateTime = (DateTime.Now).AddSeconds(float SessionTime)
                            // start tweeting
                                x.Self <! (1,1,1,1,1,1)
                            //start reception of feed
                            //keep session alive for given session duration
                                while (DateTime.Now < (SessionLogoutTime) ) do
                                printf ""
                                LogOutFlag <- true
                    

     | :? Tweet as msg -> let mutable SessionLogoutTime : DateTime = (DateTime.Now).AddSeconds(float SessionTime)  
                          let mutable TweetTime : DateTime = (DateTime.Now).AddSeconds(float 1) 
                          let mutable tweetNumber : int = 0
                          let timer = new Timers.Timer(1000.0)
                          let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
                          //printfn "%A" DateTime.Now
                          timer.Start()
                          while (DateTime.Now < SessionLogoutTime) && (LogOutFlag) && (tweetNumber < numberOfTweets ) do
                            Async.RunSynchronously event
                            let mutable tweetsCounter : int = 0
                            while (tweetsCounter <= TweetsperSecond) do
                                 let RandomHashtagSelect = System.Random().Next(10)
                                 let RandomMentionSelect = System.Random().Next(mentions.Count)
                                 UsedHashtag <- hashTags.[RandomHashtagSelect]
                                 UsedMention <- mentions.Item(RandomMentionSelect)
                                 
                                 let mutable tweet : string = "I am Tweeting" + " " + mentions.Item(RandomMentionSelect) + " " + hashTags.[RandomHashtagSelect]
                                 let mutable requestJson : RequestJsonObj = {RequestType = "Tweet" ; UserId = username ; Password = password ; TweetNumber = tweetNumber; Tweet = tweet ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = UsedMention ; HashTag = UsedHashtag}
                                 let data : string = Json.serialize requestJson
                                 //WebSocketClient.Connect(SocketObject, connection, tokenVal)
                                 TweetRequests <- TweetRequests + 1
                                 WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                                 let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                                 let mutable responseJson  =  Unchecked.defaultof<_>
                                 try
                                        responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                 with 
                                        | _ -> printfn "tweet ResponseJson Deserialization Failed" 
                                               responseJson <- {RequestType = "Tweet" ; RequestStatus = "FAILED" ; ResponseString1 = "request failed" ; ResponseString2 = "Reason : high load" }
                                 if(responseJson.RequestType = "Tweet" && responseJson.RequestStatus = "SUCCESS") then
                                             PassedTweetRequests <- PassedTweetRequests + 1
                                             printfn "Server respons : %s" responseJson.ResponseString1
                                 else 
                                             printfn "Server response : Tweeting operation cannot be done right now because of high load"                                 
                                 tweetsCounter <- tweetsCounter + 1
                                 tweetNumber <- tweetNumber + 1  
                          x.Self <! (1.5 , "none","none",WebSocketClient ,SocketObject)

                                   
     | :? StatusOfSubscription as msg ->  let mutable (userSubscriptionStatus , arg2, agr3, arg4, serverMessage) = unbox<StatusOfSubscription> msg    
                                          if (userSubscriptionStatus) then
                                            SubscriptionSuccessfulFlag <- true
                                            printfn "Server Response : %s " serverMessage
                                            SuccessfulSubscriptions <- SuccessfulSubscriptions + 1
                                            if (SuccessfulSubscriptions = SubscriptionCounter) then
                                                //start session
                                                x.Self <! (1,1,1,1)
                                          else      
                                            printfn "Server Response : %s " serverMessage     


     | :? StatusOfTweets as msg -> let mutable (int1, float1 , serverResponse) = unbox<StatusOfTweets> msg                          
                                   printfn "%s" serverResponse
                                


     | :? TweetsFromSubscriptions as msg ->let mutable (int1,int2,int3,int4,int5,int6, subscriptionUserName , tweet, WebSocketClient ,SocketObject) = unbox<TweetsFromSubscriptions> msg
                                           let RandomlyDecideToRetweet = System.Random().Next(1,5)
                                           let responseStr : string = "Subscription Feed of User : " + username + " --> " + tweet
                                           printfn "%s" responseStr                       
                                           if(RandomlyDecideToRetweet = 2) then
                                            TweetRequests <- TweetRequests + 1
                                            x.Self <! (1.5 , subscriptionUserName,tweet,WebSocketClient ,SocketObject)
                                           else
                                            printf ""

     | :? RetweetsFromSubscriptions as msg -> let mutable (int1,int2,int3,int4,int5,int6,int7, subscriptionUserName , tweet) = unbox<RetweetsFromSubscriptions> msg
                                              let responseStr : string = "Subscription Feed of User : " + username + "  -->  " + tweet    
                                              printfn "%s" responseStr                       
                                         

     | :? Retweet as msg -> let mutable (float1,subscriptionUserName , tweet,WebSocketClient ,SocketObject) = unbox<Retweet> msg
                            TweetRequests <- TweetRequests + 1
                            let mutable requestJson : RequestJsonObj = {RequestType = "Retweet" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = tweet ; FollowUserId = "none" ; SubscriptionUserName = subscriptionUserName ; Mention = "none"; HashTag = "none"}
                            let data : string = Json.serialize requestJson
                            DataQueryRequests <- DataQueryRequests + 1
                            PassedDataQueryRequests <- PassedDataQueryRequests + 1
                            WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                            let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                            let mutable responseJson  =  Unchecked.defaultof<_>
                            try
                                   responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                            with 
                                   | _ -> printfn "retweet ResponseJson Deserialization Failed" 
                                          responseJson <- {RequestType = "Tweet" ; RequestStatus = "FAILED" ; ResponseString1 = "request failed" ; ResponseString2 = "Reason : high load" }
                            if(responseJson.RequestType = "Retweet" && responseJson.RequestStatus = "SUCCESS") then
                                    PassedTweetRequests <- PassedTweetRequests + 1
                                    printfn "Server respons : %s" responseJson.ResponseString1
                            else 
                                    printfn "Server response : Retweeting operation cannot be performed right now because of Heavy load"   
                            x.Self <! (1,1,1)  
                                                        
                                                                                       

     | :? StatusOfRetweets as msg -> let mutable (int1, float1 , float2, serverResponse) = unbox<StatusOfRetweets> msg                          
                                     printfn "%s" serverResponse

     | :? HashtagQ as msg ->let mutable (float1 , hashtag) = unbox<HashtagQ> msg 
                            DataQueryRequests <- DataQueryRequests + 1
                            let mutable requestJson : RequestJsonObj = {RequestType = "HashtagQ" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = "none"; HashTag = hashtag}
                            let data : string = Json.serialize requestJson
                            WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                            let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                            let mutable responseJson  =  Unchecked.defaultof<_>
                            try
                                    responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                            with 
                                    | _ -> printfn "hashtag query request ResponseJson Deserialization Failed"
                            if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashtagQ" && responseJson.RequestStatus = "SUCCESS") then
                                    PassedDataQueryRequests <- PassedDataQueryRequests + 1
                                    x.Self <! (true,hashtag,(responseJson.ResponseString1))
                            else
                                    x.Self <! (false,hashtag,("hashtag query Reuqest made by "+ username + " for hashtag :  " + hashtag + " failed")) 
                      

     | :? MentionsQ as msg -> let mutable (float1 , float2, mention) = unbox<MentionsQ> msg
                              DataQueryRequests <- DataQueryRequests + 1
                              let mutable requestJson : RequestJsonObj = {RequestType = "HashtagQ" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none" ; Mention = mention; HashTag = "none"}
                              let data : string = Json.serialize requestJson
                              WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                              let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                              let mutable responseJson  =  Unchecked.defaultof<_>
                              try
                                     responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                              with 
                                     | _ -> printfn "mention query request ResponseJson Deserialization Failed"
                              if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashtagQ" && responseJson.RequestStatus = "SUCCESS") then
                                      PassedDataQueryRequests <- PassedDataQueryRequests + 1
                                      x.Self <! (true,true,mention,(responseJson.ResponseString1))
                              else
                                      x.Self <! (false,false,mention,("mention query Reuqest made by "+ username + " for mention :  " + mention + " failed")) 
                                 
                                     

     | :? GetUserTweetQ as msg ->  let mutable (float1 , float2, float3, userId) = unbox<GetUserTweetQ> msg
                                   DataQueryRequests <- DataQueryRequests + 1
                                   let mutable requestJson : RequestJsonObj = { RequestType = "GetUserTweetQ" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = userId ; Mention = "none" ; HashTag = "none"}
                                   let data : string = Json.serialize requestJson
                                   WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                                   let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                                   let mutable responseJson  =  Unchecked.defaultof<_>
                                   try
                                            responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                                   with 
                                            | _ -> printfn "getusertweets query query request ResponseJson Deserialization Failed"
                                   if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "HashtagQ" && responseJson.RequestStatus = "SUCCESS") then
                                            PassedDataQueryRequests <- PassedDataQueryRequests + 1
                                            x.Self <! (true,true,true,userId,(responseJson.ResponseString1))
                                   else
                                            x.Self <! (false,false,false, userId,("getusertweets query Reuqest made by "+ username + " for userid :  " + userId + " failed"))
                

     | :? HashTagQResponse as msg -> let mutable (queryBoolStatus , hashtag , responseString) = unbox<HashTagQResponse> msg 
                                     if queryBoolStatus then
                                             printfn "Server Response : All Tweets Containing the Hashtag : %s are : %s" hashtag responseString
                                     else
                                            printfn "Server Response : No Tweet Found which Contains the Hashtag : %s " hashtag 
                                            Q1Flag <- true

     | :? MentionsQResponse as msg -> let mutable (queryBoolStatus , bool2, mention , responseString) = unbox<MentionsQResponse> msg 
                                      if queryBoolStatus then
                                              printfn "Server Response : All Tweets Containing the mention : %s are : %s" mention responseString
                                      else
                                              printfn "Server Response : No Tweet Found which Contains the mention : %s " mention
                                              Q2Flag <- true

     | :? GetUserTweetsResponse as msg -> let mutable (queryBoolStatus ,bool2,bool3, username , responseString) = unbox<GetUserTweetsResponse> msg 
                                          if queryBoolStatus then
                                              printfn "Server Response : All Tweets from User with UserId: %s are  :  %s" username responseString
                                          else
                                              printfn "Server Response : No Tweet Found from User with UserId: %s" username    
                                          Q3Flag <- true                                   

     | :? LogoutUser as msg -> if(LogoutWatchFlag = 1) then   
                                  x.Self <! (1.7 , UsedHashtag)
                                  x.Self <! (1.7 ,1.4, UsedMention)
                                  x.Self <! (1.7 ,1.5 ,2.3, "user6")   
                                  LogoutWatchFlag <- 0 
                                  let timer2 = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
                                  let event2 = Async.AwaitEvent (timer2.Elapsed) |> Async.Ignore
                                  timer2.Start()
                                  Async.RunSynchronously event2
                                
                               SuccessfullyLoggedOutUsers <- SuccessfullyLoggedOutUsers + 1
                               LogoutRequests <- LogoutRequests + 1
                               let mutable requestJson : RequestJsonObj = { RequestType = "Logout" ; UserId = username ; Password = password ; TweetNumber = 0; Tweet = "none" ; FollowUserId = "none" ; SubscriptionUserName = "none"  ; Mention = "none" ; HashTag = "none"}
                               let data : string = Json.serialize requestJson
                               WebSocketClient.Send(SocketObject, data , tokenVal).Wait(tokenVal)
                               let responseJsonString : string = (WebSocketClient.Receive(SocketObject, tokenVal)).Result
                               let mutable responseJson  =  Unchecked.defaultof<_>
                               try
                                   responseJson  <-  Json.deserialize<ResponseJsonObj>(responseJsonString)
                               with 
                                  | _ -> printfn "logout request ResponseJson Deserialization Failed"
                               if (responseJson <> Unchecked.defaultof<_> && responseJson.RequestType = "Logout" && responseJson.RequestStatus = "SUCCESS") then
                                PassedLogoutRequests <- PassedLogoutRequests + 1
                                x.Self <! (true,true,true,(responseJson.ResponseString1))
                               else
                                x.Self <! (false,false,false,("logout Reuqest made by user : "+ username + " failed"))   

                            
     
     | :? StatusOfLogout as msg -> let mutable (userLogoutStatus , arg2, arg3, serverMessage) = unbox<StatusOfLogout> msg       
                                   printfn "Server Response : %s " serverMessage 
                                                                       
                                       
                                 

     | :? ServerResponse as msg -> printfn "%s" msg

     | _ -> failwith "Message Format is invalid for child node"



let userActorRefArray : IActorRef array = Array.zeroCreate NumberOfUsers
let tweetReceiverRefArray : IActorRef array = Array.zeroCreate NumberOfUsers
printfn "Hello....."
//Create and simulate user and start their registration
for i in 1 .. NumberOfUsers do
    let mutable userId : string = "user" + string(i)
    let mutable tweetReceiverId : string = "tweetreceiver" + string(i)
    mentions.Add("@" + userId)
    let mutable password = userId + string(123)
    let mutable sessionTimeInSeconds : int = SessionTimeFOrEachUserInSeconds  // time interval between user login and logout
    let mutable subscriptionList : List<string> = ZipFDistributedSubscriptionMap.Item(userId)
    let mutable numberOfTweetsForUser : int = ZipFDistributedTweetMap.Item(userId)
    let mutable SubscriptionCounter : int = subscriptionList.Count
    let mutable TweetsperSecond : int = Convert.ToInt32(Math.Ceiling( (float numberOfTweetsForUser) / (float sessionTimeInSeconds) ) )
    printfn "userId : %s SubscriptionCounter : %i numberOfTweetsForUser : %i TweetsperSecond : %i" userId SubscriptionCounter numberOfTweetsForUser TweetsperSecond
    userActorRefArray.[i-1] <- system.ActorOf(Props(typedefof<UserActor>,[|box userId;  box password ; box sessionTimeInSeconds ; box SubscriptionCounter ; box subscriptionList ;box numberOfTweetsForUser ;  box TweetsperSecond |]),userId)
    printfn "User has been created with userId %s" userId 
    
    userActorRefArray.[i-1]  <! (1)


let timer = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
timer.Start()
Async.RunSynchronously event
ShutDownExecution <- true

let timer2 = new Timers.Timer(float (SessionTimeFOrEachUserInSeconds * 1000))
let event2 = Async.AwaitEvent (timer2.Elapsed) |> Async.Ignore
timer2.Start()
Async.RunSynchronously event2
printfn "Session completed"


while not (ShutDownExecution) do
     ignore ()

system.Terminate()


printfn "*******Simulation Over : System Shutting Down*******"

printfn "*******  PERFORMANCE Stats *********"

printfn "Number of Users simulated  : %i" NumberOfUsers
printfn "Session Time Between Login/Logout For each user : %i" SessionTimeFOrEachUserInSeconds

printfn "Number of Registration requests : %i" RegistrationRequests
printfn "Number of Login requests : %i" LoginRequests
printfn "Number of Follow requests: %i" FollowRequests
printfn "Number of Tweet/Retweet requests: %i" TweetRequests
printfn "Number of Logout requests: %i" LogoutRequests
printfn "Number of dataQueryRequests(Hashtag/Mention/GetUserstweets) requests: %i" DataQueryRequests

printfn "Successful Registration requests  : %i" PassedRegistrations
printfn "Successful Login requests : %i" PassedLoginRequests
printfn "Successful Follow requests: %i" PassedFollowRequests
printfn "Successful Tweet/Retweet requests: %i" PassedTweetRequests
printfn "Successful Logout requests: %i" PassedLogoutRequests
printfn "Successful dataQueryRequests(Hashtag/Mention/GetUserstweets) requests: %i" PassedDataQueryRequests

printfn "Failed Registration requests: %i" (RegistrationRequests - PassedRegistrations)
printfn "Failed Login requests: %i" (LoginRequests - PassedLoginRequests )
printfn "Failed Follow requests: %i" (FollowRequests - PassedFollowRequests)
printfn "Failed Tweet/Retweet requests: %i" (TweetRequests - PassedTweetRequests)
printfn "Failed Logout requests: %i" (LogoutRequests - PassedLogoutRequests)
printfn "Failed dataQueryRequests(Hashtag/Mention/GetUserstweets) requests: %i" (DataQueryRequests - PassedDataQueryRequests)






