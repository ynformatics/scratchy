using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using Scratchy.Services;

namespace Scratchy
{
    [Activity(Label = "Scratchy", MainLauncher = true, Icon = "@drawable/ic_launcher", Theme = "@style/Theme")]
    public class MainActivity : Activity
    {

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            var learn = FindViewById<Button>(Resource.Id.learnButton);
            var match = FindViewById<Button>(Resource.Id.matchButton);

            var songid = FindViewById<EditText>(Resource.Id.songid);        

            learn.Click += (sender, args) => SendAudioCommand(ScratchyService.ActionLearn,
                songid.Text);
            match.Click += (sender, args) => SendAudioCommand(ScratchyService.ActionMatch);

            // initialise
            SendAudioCommand(ScratchyService.ActionStop);
        }

        private void SendAudioCommand(string action, string data = "")
        {
            var intent = new Intent(action);
            intent.PutExtra("data", data);
            intent.SetPackage(this.PackageName);
            StartService(intent);
        }

    }
}

