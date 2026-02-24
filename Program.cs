using Winpipe.Sound;


using var audio = new Audio();
Console.WriteLine("Recording audio...");
audio.StartRecording();
Console.WriteLine("Audio recording stopped.");
