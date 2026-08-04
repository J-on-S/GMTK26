using System;
using System.Collections.Generic;
using UnityEngine;

public class DoctorDialogueTexts //simple static class as it could expand on the behaviour of the player and more to give personalized text but this will do for now. 
{
    public static List<string> acceptingTexts = new() { "Dude thanks for giving me that." ,
     "Great, thank you" , 
     "Wow you actually found it?",
     "Appreciated, now get back to work",
     "Your training is paying off!"};


    public static List<string> failureTexts = new() { "Nah man wrong tool",
    "That's not what I asked..." ,
    "Does that look like what I asked?",
    "Are you blind or something?",
    "Don't you remember your training?",
    "It's not today that you'll be replacing me"};

    public static string getRandomAcceptingText(Request request , Item ReceivedItem)
    {
       int random = UnityEngine.Random.Range(0,acceptingTexts.Count );
       return acceptingTexts[random];
    }
    public static string getRandomFailureText(Request request , Item ReceivedItem)
    {
       int random = UnityEngine.Random.Range(0,acceptingTexts.Count );
       return failureTexts[random];
    }


    
}