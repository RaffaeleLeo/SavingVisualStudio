﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static Boggle.DataModels;
using static System.Net.HttpStatusCode;

namespace Boggle
{
    public class BoggleService : IBoggleService
    {

        //Poor Man's database
        private static readonly object sync = new object();
        private readonly static Dictionary<String, Username> users = new Dictionary<String, Username>();
        private readonly static Dictionary<int, Game> games = new Dictionary<int, Game>();
        private readonly static Queue<PendingGame> pendingGames = new Queue<PendingGame>();


        //Connection to Rich Man's Database
        private static string BoggleDB;
        static BoggleService()
        {
            BoggleDB = ConfigurationManager.ConnectionStrings["BoggleDB"].ConnectionString;
        }


        /// <summary>
        /// The most recent call to SetStatus determines the response code used when
        /// an http response is sent.
        /// </summary>
        /// <param name="status"></param>
        private static void SetStatus(HttpStatusCode status)
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = status;
        }

        /// <summary>
        /// Returns a Stream version of index.html.
        /// </summary>
        /// <returns></returns>
        public Stream API()
        {
            SetStatus(OK);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";
            return File.OpenRead(AppDomain.CurrentDomain.BaseDirectory + "index.html");
        }

        /// <summary>
        /// This handles a register user request and checks if the username is null and returns
        /// a forbidden response if that is true and returns a userID is not.
        /// </summary>
        /// <param name="nickname"></param>
        /// <returns></returns>
        public string Register(Username name)
        {

            if (name.Nickname == null || name.Nickname.Trim().Length == 0 || name.Nickname.Trim().Length > 50)
            {
                SetStatus(Forbidden);
                return null;
            }

            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                // Connections must be opened
                conn.Open();

                // Database commands should be executed within a transaction.  When commands 
                // are executed within a transaction, either all of the commands will succeed
                // or all will be canceled.  You don't have to worry about some of the commands
                // changing the DB and others failing.
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    // An SqlCommand executes a SQL statement on the database.  In this case it is an
                    // insert statement.  The first parameter is the statement, the second is the
                    // connection, and the third is the transaction.  
                    //
                    // Note that I use symbols like @UserID as placeholders for values that need to appear
                    // in the statement.  You will see below how the placeholders are replaced.  You may be
                    // tempted to simply paste the values into the string, but this is a BAD IDEA that violates
                    // a cardinal rule of DB Security 101.  By using the placeholder approach, you don't have
                    // to worry about escaping special characters and you don't have to worry about one form
                    // of the SQL injection attack.
                    using (SqlCommand command =
                        new SqlCommand("insert into Users (UserID, Nickname) values(@UserID, @Nickname)",
                                        conn,
                                        trans))
                    {
                        // We generate the userID to use.
                        string userID = Guid.NewGuid().ToString();

                        // This is where the placeholders are replaced.
                        command.Parameters.AddWithValue("@UserID", userID);
                        command.Parameters.AddWithValue("@Nickname", name.Nickname.Trim());
                        

                        // This executes the command within the transaction over the connection.  The number of rows
                        // that were modified is returned.
                        if (command.ExecuteNonQuery() != 1)
                        {
                            throw new Exception("Query failed unexpectedly");
                        }
                        SetStatus(Created);

                        // Immediately before each return that appears within the scope of a transaction, it is
                        // important to commit the transaction.  Otherwise, the transaction will be aborted and
                        // rolled back as soon as control leaves the scope of the transaction. 
                        trans.Commit();
                        return userID;
                    }
                }


            }
        }

        public int JoinGame(GameRequest request)
        {
            /**
            using (SqlConnection conn = new SqlConnection(BoggleDB))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {

                    // Here, the SqlCommand is a select query.  We are interested in whether item.UserID exists in
                    // the Users table.
                    using (SqlCommand command = new SqlCommand("select UserID from Users where UserID = @UserID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserID", request.UserToken);

                        // This executes a query (i.e. a select statement).  The result is an
                        // SqlDataReader that you can use to iterate through the rows in the response.
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // In this we don't actually need to read any data; we only need
                            // to know whether a row was returned.
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                reader.Close();
                                trans.Commit();
                                return -1;
                            }
                        }
                    }

                    if (request.TimeLimit < 5 || request.TimeLimit > 120)
                    {
                        SetStatus(Forbidden);
                        return 0;
                    }

                    using (SqlCommand command = new SqlCommand("select GameID from Games where GameID = @UserID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserID", request.UserToken);

                        // This executes a query (i.e. a select statement).  The result is an
                        // SqlDataReader that you can use to iterate through the rows in the response.
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // In this we don't actually need to read any data; we only need
                            // to know whether a row was returned.
                            if (!reader.HasRows)
                            {
                                SetStatus(Forbidden);
                                reader.Close();
                                trans.Commit();
                                return -1;
                            }
                        }
                    }
                }
            }
            **/

            
            lock (sync)
            {
                int GameID = 0;

                if(!users.ContainsKey(request.UserToken))
                {
                    SetStatus(Forbidden);
                    return 0;
                }

                if(request.TimeLimit < 5 || request.TimeLimit > 120)
                {
                    SetStatus(Forbidden);
                    return 0;
                }
                
                //check if there is a pending game they could join
                if(pendingGames.Count > 0)
                {
                    //check if user is already in queue
                    if(pendingGames.Peek().UserToken == request.UserToken)
                    {
                        SetStatus(Conflict);
                        return 0;
                    }

                    SetStatus(Created);
                    GameID = pendingGames.Dequeue().GameID;

                    //Initialize the new game propeties
                    BoggleBoard board = new BoggleBoard();
                    games[GameID].GameState = "active";
                    games[GameID].Board = board.ToString();
                    games[GameID].BoggleBoard = board;  
                    
                    //Set the time limit and save the start time
                    int oldTimeLimit = games[GameID].TimeLimit;
                    int newTimeLimit = (oldTimeLimit + request.TimeLimit) / 2;
                    games[GameID].TimeLimit = newTimeLimit;
                    games[GameID].TimeLeft = newTimeLimit;
                    int currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    games[GameID].StartTime = currentTime;

                    //Set up Second Player
                    Player2 newPlayer = new Player2();
                    games[GameID].Player2 = newPlayer;
                    games[GameID].Player2.Nickname = users[request.UserToken].Nickname;
                    games[GameID].Player2.Score = 0;
                    games[GameID].Player2.UserToken = request.UserToken;
                    WordsPlayedObject player2Words = new WordsPlayedObject();
                    games[GameID].Player2.PlayerTwoWords = player2Words;
                    List<WordPlayed> WordsPlayed = new List<WordPlayed>();
                    player2Words.WordsPlayed = WordsPlayed;

                    return GameID;
                    
                }

                //if else make a new pending game with unique Game ID
                else
                {
                    SetStatus(Accepted);
                    Game newGame = new Game();

                    //initialize the pending game and the first player
                    Player1 newPlayer = new Player1();
                    newGame.Player1 = newPlayer;
                    newGame.Player1.Nickname = users[request.UserToken].Nickname;
                    newGame.Player1.Score = 0;
                    

                    newGame.GameState = "Pending";
                    newGame.TimeLimit = request.TimeLimit;

                    //create random Game ID
                    Random random = new Random();
                    bool findingID = true;
                    while (findingID)
                    {

                        GameID = random.Next(1, 1000);
                        if(!games.ContainsKey(GameID))
                        {
                            findingID = false;
                        }
                    }

                    //add the game to the database
                    games.Add(GameID, newGame);

                    //create the pending game object to enqueue
                    PendingGame pending = new PendingGame();
                    pending.GameID = GameID;
                    pending.UserToken = request.UserToken;

                    //add this gameID to the pending Queue
                    pendingGames.Enqueue(pending);

                    //save this game id within the first player
                    games[GameID].Player1.UserToken = request.UserToken;
                    WordsPlayedObject player1Words = new WordsPlayedObject();
                    games[GameID].Player1.PlayerOneWords = player1Words;
                    List<WordPlayed> WordsPlayed = new List<WordPlayed>();
                    player1Words.WordsPlayed = WordsPlayed;

                    return GameID;
                }

            }
        }

        /// <summary>
        /// Demo.  You can delete this.
        /// </summary>
        public string WordAtIndex(int n)
        {
            if (n < 0)
            {
                SetStatus(Forbidden);
                return null;
            }

            string line;
            using (StreamReader file = new System.IO.StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (n == 0) break;
                    n--;
                }
            }

            if (n == 0)
            {
                SetStatus(OK);
                return line;
            }
            else
            {
                SetStatus(Forbidden);
                return null;
            }
        }

        /// <summary>
        /// gets the user out of the queue
        /// </summary>
        /// <param name="userID"></param>
        public void CancelQueue(CancelGameRequest userID)
        {
            if(!users.ContainsKey(userID.UserToken))
            {
                SetStatus(Forbidden);
            }

            else if(pendingGames.Count == 0)
            {
                SetStatus(Forbidden);
            }

            else if(!(pendingGames.Peek().UserToken == userID.UserToken))
            {
                SetStatus(Forbidden);
            }

            else
            {
                games.Remove(pendingGames.Peek().GameID);
                pendingGames.Dequeue();
                SetStatus(OK);
                return;
            }


        }

        public int PlayWord(WordAttempt attempt, string GameID)
        {
            int GameID_int = Int32.Parse(GameID);
            

            //If Word is null or empty or longer than 30 characters when trimmed, responds with response code 403 (Forbidden).
            if (attempt.Word == null || attempt.Word.Length <= 0 || attempt.Word.Length > 30)
            {
                SetStatus(Forbidden);
                return 0;
            }
            //if GameID or UserToken is invalid, responds with response code 403 (Forbidden).
            else if (!games.ContainsKey(GameID_int) || !users.ContainsKey(attempt.UserToken))
            {
                SetStatus(Forbidden);
                return 0;
            }
            //if UserToken is not a player in the game identified by GameID, responds with response code 403 (Forbidden).
            if (!games[GameID_int].Player1.UserToken.Equals(attempt.UserToken))
            {
                if (!games[GameID_int].Player2.UserToken.Equals(attempt.UserToken))
                {
                    SetStatus(Forbidden);
                    return 0;
                }
                
            }
            //check if its an actual word
            using (StreamReader words = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "dictionary.txt"))
            {
                string validWord;
                bool isActualWord = false;
                while((validWord = words.ReadLine()) != null)
                {
                    if(attempt.Word.ToUpper() == validWord)
                    {
                        isActualWord = true;
                        break;
                    }
                }

                if(isActualWord == false)
                {
                    SetStatus(OK);
                    WordPlayed word = new WordPlayed();

                    if (games[GameID_int].Player1.UserToken == attempt.UserToken)
                    {
                        word.Word = attempt.Word;
                        word.Score = -1;
                        games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                        games[GameID_int].Player1.Score = games[GameID_int].Player1.Score - 1;
                    }
                    else
                    {
                        word.Word = attempt.Word;
                        word.Score = -1;
                        games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                        games[GameID_int].Player2.Score = games[GameID_int].Player2.Score - 1;
                    }
                    return -1;
                }
            }

            int timeLimit = games[GameID_int].TimeLimit;
            int startTime = games[GameID_int].StartTime;
            int currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            int timeElapsed = currentTime - startTime;
            int timeLeft = timeLimit - timeElapsed;

            //if the game state is anything other than "active", responds with response code 409 (Conflict).
            if (currentTime - startTime >= timeLimit)
            {
                SetStatus(Conflict);
                return 0;
            }
            //records the trimmed Word as being played by UserToken in the game identified by GameID. 
            //Returns the score for Word in the context of the game (e.g. if Word has been played before the score is zero). 
            //Responds with status 200 (OK). Note: The word is not case sensitive.

            //checks to see if the word entered is contained on the boggle board
            else if (games[GameID_int].BoggleBoard.CanBeFormed(attempt.Word))
            {
                //check to see which player entered a word
                if(games[GameID_int].Player1.UserToken == attempt.UserToken)
                {
                    //if player one entered a word loop through the words that he/she already entered
                    //and return the score of that word
                    foreach(WordPlayed playedWord in games[GameID_int].Player1.PlayerOneWords.WordsPlayed)
                    {
                        //if the user already entered a word, return a score of 0
                        if(playedWord.Word == attempt.Word)
                        {
                            SetStatus(OK);
                            return 0;
                        }
                    }

                    //else return the score of the word based on its length
                    int length = attempt.Word.Length;
                    WordPlayed word = new WordPlayed();
                    switch (length)
                    {
                        case 3:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 1;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 1;
                            return 1;
                        case 4:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 1;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 1;
                            return 1;
                        case 5:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 2;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 2;
                            return 2;
                        case 6:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 3;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 3;
                            return 3;
                        case 7:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 4;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 4;
                            return 4;
                        case 8:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 11;
                            games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                            games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 11;
                            return 11;
                    }
                    if (length > 8)
                    {
                        SetStatus(OK);
                        word.Word = attempt.Word;
                        word.Score = 1;
                        games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                        games[GameID_int].Player1.Score = games[GameID_int].Player1.Score + 11;
                        return 11;
                    }
                    //should not reach
                    return -2;
                }
                else
                {
                    //if player two entered a word loop through the words that he/she already entered
                    //and return the score of that word
                    foreach (WordPlayed attemptedWord in games[GameID_int].Player2.PlayerTwoWords.WordsPlayed)
                    {
                        //if the user already entered a word, return a score of 0
                        if (attemptedWord.Word == attempt.Word)
                        {
                            SetStatus(OK);
                            return 0;
                        }
                    }

                    //else return the score of the word based on its length
                    int length = attempt.Word.Length;
                    WordPlayed word = new WordPlayed();
                    switch (length)
                    {
                        case 3:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 1;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 1;
                            return 1;
                        case 4:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 1;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 1;
                            return 1;
                        case 5:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 2;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 2;
                            return 2;
                        case 6:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 3;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 3;
                            return 3;
                        case 7:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 4;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 4;
                            return 4;
                        case 8:
                            SetStatus(OK);
                            word.Word = attempt.Word;
                            word.Score = 11;
                            games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                            games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 11;
                            return 11;
                    }
                    if (length > 8)
                    {
                        SetStatus(OK);
                        word.Word = attempt.Word;
                        word.Score = 1;
                        games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                        games[GameID_int].Player2.Score = games[GameID_int].Player2.Score + 11;
                        return 11;
                    }
                    //should not reach
                    return -2;
                }
            }
            else
            {
                SetStatus(OK);
                WordPlayed word = new WordPlayed();

                if (games[GameID_int].Player1.UserToken == attempt.UserToken)
                {
                    word.Word = attempt.Word;
                    word.Score = -1;
                    games[GameID_int].Player1.PlayerOneWords.WordsPlayed.Add(word);
                    games[GameID_int].Player1.Score = games[GameID_int].Player1.Score - 1;
                }
                else
                {
                    word.Word = attempt.Word;
                    word.Score = -1;
                    games[GameID_int].Player2.PlayerTwoWords.WordsPlayed.Add(word);
                    games[GameID_int].Player2.Score = games[GameID_int].Player2.Score - 1;
                }

                return -1;
            }
        }

        public Game GetStatus(string GameID, string brief)
        {
            int GameID_int;

            try
            {
                GameID_int = Int32.Parse(GameID);

                if (!games.ContainsKey(GameID_int))
                {
                    SetStatus(Forbidden);
                    return null;
                }
            }
            catch(Exception ex)
            {
                SetStatus(Forbidden);
                return null;
            }

           


            //check if brief was a paramater or not
            if (brief == null)
            {
                SetStatus(OK);

                int timeLimit = games[GameID_int].TimeLimit;
                int startTime = games[GameID_int].StartTime;
                int currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                int timeElapsed = currentTime - startTime;
                int timeLeft = timeLimit - timeElapsed;

                if (currentTime - startTime >= timeLimit)
                {
                    //game is complete
                    Game gameComplete = new Game();

                    gameComplete.GameState = "completed";
                    gameComplete.TimeLeft = 0;
                    gameComplete.TimeLimit = timeLimit;

                    gameComplete.Board = games[GameID_int].Board;

                    Player1 playerOneComplete = new Player1();
                    gameComplete.Player1 = playerOneComplete;
                    Player2 playerTwoComplete = new Player2();
                    gameComplete.Player2 = playerTwoComplete;

                    playerOneComplete.Score = games[GameID_int].Player1.Score;
                    playerOneComplete.Nickname = games[GameID_int].Player1.Nickname;
                    WordsPlayedObject playerOneWords = new WordsPlayedObject();
                    playerOneComplete.PlayerOneWords = playerOneWords;
                    List<WordPlayed> WordsPlayed = new List<WordPlayed>();
                    playerOneWords.WordsPlayed = WordsPlayed;
                    playerOneWords.WordsPlayed = games[GameID_int].Player1.PlayerOneWords.WordsPlayed;

                    playerTwoComplete.Score = games[GameID_int].Player2.Score;
                    playerTwoComplete.Nickname = games[GameID_int].Player2.Nickname;
                    WordsPlayedObject playerTwoWords = new WordsPlayedObject();
                    playerTwoComplete.PlayerTwoWords = playerTwoWords;
                    List<WordPlayed> WordsPlayed2 = new List<WordPlayed>();
                    playerTwoWords.WordsPlayed = WordsPlayed2;
                    playerTwoWords.WordsPlayed = games[GameID_int].Player2.PlayerTwoWords.WordsPlayed;

                    return gameComplete;
                }
                else
                {
                    //game is active
                    Game gameActive = new Game();

                    gameActive.GameState = "active";
                    gameActive.TimeLeft = timeLeft;
                    gameActive.TimeLimit = timeLimit;

                    gameActive.Board = games[GameID_int].Board;

                    Player1 playerOneActive = new Player1();
                    gameActive.Player1 = playerOneActive;
                    Player2 PlayerTwoActive = new Player2();
                    gameActive.Player2 = PlayerTwoActive;

                    playerOneActive.Score = games[GameID_int].Player1.Score;
                    playerOneActive.Nickname = games[GameID_int].Player1.Nickname;
                    
                    PlayerTwoActive.Score = games[GameID_int].Player2.Score;
                    PlayerTwoActive.Nickname = games[GameID_int].Player2.Nickname;
                    

                    return gameActive;

                }
            }
            if(brief == "yes")
            {
                SetStatus(OK);

                int timeLimit = games[GameID_int].TimeLimit;
                int startTime = games[GameID_int].StartTime;
                int currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                int timeElapsed = currentTime - startTime;
                int timeLeft = timeLimit - timeElapsed;

                if(timeElapsed >= timeLimit)
                {
                    //game is complete
                    Game gameComplete = new Game();

                    gameComplete.GameState = "completed";
                    gameComplete.TimeLeft = 0;

                    Player1 playerOneComplete = new Player1();
                    gameComplete.Player1 = playerOneComplete;
                    Player2 playerTwoComplete = new Player2();
                    gameComplete.Player2 = playerTwoComplete;

                    playerOneComplete.Score = games[GameID_int].Player1.Score;
                    playerTwoComplete.Score = games[GameID_int].Player2.Score;

                    return gameComplete;
                }
                else
                {
                    //game is active
                    Game gameComplete = new Game();

                    gameComplete.GameState = "active";
                    gameComplete.TimeLeft = timeLeft;

                    Player1 playerOneComplete = new Player1();
                    gameComplete.Player1 = playerOneComplete;
                    Player2 playerTwoComplete = new Player2();
                    gameComplete.Player2 = playerTwoComplete;

                    playerOneComplete.Score = games[GameID_int].Player1.Score;
                    playerTwoComplete.Score = games[GameID_int].Player2.Score;

                    return gameComplete;
                }
            }

            return null;

        }
    }
}
