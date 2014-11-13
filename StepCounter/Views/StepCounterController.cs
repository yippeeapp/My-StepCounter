using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.CodeDom.Compiler;
using StepCounter.Helpers;
using System.Drawing;
using MonoTouch.CoreMotion;
using StepCounter.Views;
using System.Globalization;
using MonoTouch.ObjCRuntime;
using MonoTouch.CoreAnimation;
using Xamarin;
using System.Collections.Generic;

namespace StepCounter
{
	partial class StepCounterController : UIViewController
	{
        private readonly StepManager _stepManager;
        private ProgressView _progressView;

		public StepCounterController (IntPtr handle) : base (handle)
		{
            _stepManager = new StepManager();
        }

        private static string DateString
        {
            get
            {
                string day = DateTime.Now.DayOfWeek.ToString();
                string month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
                int dayNum = DateTime.Now.Day;
                return day + " " + dayNum + " " + month;
            }
        }
            
        public void RefreshView()
        {
            _stepManager.ForceUpdate();
        }

        //Private Methods
        private void ConvertDistance()
        {
            Settings.DistanceIsMetric = Settings.DistanceIsMetric == false;
            _stepManager.ForceUpdate();
        }

        private void TodaysStepCountChanged(int stepCount)
        {
            //Setup Animation
            var stepCountAnimation = new CATransition ();
            stepCountAnimation.Duration = 0.7f;
            stepCountAnimation.Type = "kCATransitionFade";
            stepCountAnimation.TimingFunction = CAMediaTimingFunction.FromName (CAMediaTimingFunction.EaseInEaseOut);

            lblStepCount.Layer.AddAnimation(stepCountAnimation, "changeTextTransition");
            lblStepCount.Text = stepCount.ToString();

            var percentageCountAnimation = new CATransition ();
            percentageCountAnimation.Duration = 0.7f;
            percentageCountAnimation.Type = "kCATransitionFade";
            percentageCountAnimation.TimingFunction = CAMediaTimingFunction.FromName (CAMediaTimingFunction.EaseInEaseOut);
            lblPercentage.Layer.AddAnimation(percentageCountAnimation, "changeTextTransition");

            if (stepCount == 0)
            {
                lblCalories.Text = "";
            }
            else
            {
                lblCalories.Text = Conversion.CaloriesBurnt(Conversion.StepsToMiles(stepCount)) + " Calories";
            }

            //Percentage Complete Label
            if (stepCount <= 10000)
            {
                lblPercentage.Text = Conversion.StepCountToPercentage(stepCount) + "% Complete";
            }
            else
            {
                lblPercentage.Text = "Completed";
            }

            //Date
            lblDate.Text = DateString;

            //Distance 
            if (Settings.DistanceIsMetric == false)
            {
                btnDistance.SetTitle(Conversion.StepsToMiles(stepCount).ToString("N2") + " mi", UIControlState.Normal);
            }
            else
            {
                btnDistance.SetTitle(Conversion.StepsToKilometers(stepCount).ToString("N2") + " km",
                    UIControlState.Normal);
            }

            //Update progress filler view
            _progressView.SetStepCount(stepCount);
            if (stepCount <= 10000)
            {
                AnimateToPercentage(Conversion.StepCountToPercentage(stepCount));
            }
            else
            {
                 AnimateToPercentage(100); //I want to show something...
            }
        }

        void AnimateToPercentage(double targetPercentage)
        {
            var duration = 2.0 / 100 * targetPercentage;
            var inverseDuration = 100 - (100/100*duration);

            UIView.AnimateNotify(inverseDuration, 0.0, 1.6f, 2.0f, 0,  () => {
                _progressView.Frame = GetTargetPositionFromPercent(targetPercentage);
            }, null);

            _progressView.SetPercentage((byte) targetPercentage); //Stops flashing through red
        }

        void SetupParallax()
        {
            var xCenterEffect = new UIInterpolatingMotionEffect("center.x",
                UIInterpolatingMotionEffectType.TiltAlongHorizontalAxis)
                {
                    MinimumRelativeValue = new NSNumber(-20),
                    MaximumRelativeValue = new NSNumber(20)
                };

            var yCenterEffect = new UIInterpolatingMotionEffect("center.y",
                UIInterpolatingMotionEffectType.TiltAlongVerticalAxis)
                {
                    MinimumRelativeValue = new NSNumber(-20),
                    MaximumRelativeValue = new NSNumber(20)
                };

            var effectGroup = new UIMotionEffectGroup
                {
                    MotionEffects = new UIMotionEffect[] {xCenterEffect, yCenterEffect}
                };

            lblTodayYouveTaken.AddMotionEffect(effectGroup);
            lblStepCount.AddMotionEffect(effectGroup);
            lblSteps.AddMotionEffect(effectGroup);
            lblCalories.AddMotionEffect(effectGroup);
            lblDate.AddMotionEffect(effectGroup);
            lblPercentage.AddMotionEffect(effectGroup);
            btnDistance.AddMotionEffect(effectGroup);
        }
            
        RectangleF GetTargetPositionFromPercent(double percentageComplete)
        {
            var height = View.Frame.Size.Height;
            var inversePercentage = 100 - (100/100*percentageComplete);
            var position = (height/100)*inversePercentage;

            return new RectangleF(0, (float) position, _progressView.Frame.Size.Width, View.Frame.Size.Height);
        }

        #region View lifecycle

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            SetupParallax();
            View.UserInteractionEnabled = true;

            View.AddGestureRecognizer(new UISwipeGestureRecognizer(gesture =>
                {
                    _stepManager.StartCountingFrom(DateTime.Now);
                    Insights.Track("User Swiped", new Dictionary<string, string> {
                        {"Direction", "Down"}
                    });

                }) {Direction = UISwipeGestureRecognizerDirection.Down,});

            View.AddGestureRecognizer(new UISwipeGestureRecognizer(gesture =>
                {
                    _stepManager.StartCountingFrom(DateTime.Today);
                    Insights.Track("User Swiped", new Dictionary<string, string> {
                        {"Direction", "Up"}
                    });
                }) {Direction = UISwipeGestureRecognizerDirection.Up,});


            // Perform any additional setup after loading the view, typically from a nib.
            _progressView = new ProgressView();
            _progressView.Frame = this.View.Frame;
            this.View.AddSubview(_progressView);
            this.View.SendSubviewToBack(_progressView);
            _stepManager.DailyStepCountChanged += TodaysStepCountChanged;
           
            if (CMStepCounter.IsStepCountingAvailable == false)
            {
                var unsupportedDevice = new UnsupportedDevice();
                unsupportedDevice.View.Frame = View.Frame;
                View.Add(unsupportedDevice.View);
            }
                
            btnDistance.SetTitleColor(UIColor.White, UIControlState.Normal);
            btnDistance.SetTitleColor(UIColor.White, UIControlState.Selected);
            btnDistance.SetTitleColor(UIColor.White, UIControlState.Highlighted);
            lblDate.Text = DateString;

        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);
        }


        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
        }

        public override UIStatusBarStyle PreferredStatusBarStyle()
        {
            return UIStatusBarStyle.LightContent;
        }


        #endregion

        partial void btnShare_TouchUpInside(UIButton sender)
        {
            UIGraphics.BeginImageContext(View.Frame.Size);
            View.DrawViewHierarchy(View.Frame, true);
            UIImage image = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();

            var shareText = string.Format("I've taken {0} steps today using #MyStepCounter!", lblStepCount.Text);
            var social = new UIActivityViewController(new NSObject[] { new NSString(shareText), image}, 
                new UIActivity[] { new UIActivity() });
            PresentViewController(social, true, null);

            Insights.Track("SharedClicked", new Dictionary<string, string> {
                {"Current Step Count", lblStepCount.Text}
            });
        }

        partial void btnDistance_TouchUpInside(UIButton sender)
        {
            ConvertDistance();
            var length = "";
            if (!Settings.DistanceIsMetric)
            {
                length = "Miles";
            }
            else
            {
                length = "Kilometers";
            }

            Insights.Track("Length Updated", new Dictionary<string, string> {
                {"Distance Measured in", length},
            });

        }

	}
}
