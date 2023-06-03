using System;
using System.Threading;
using InputInterceptorNS;

namespace KeyRemapper;

public class MouseController : IDisposable
{
    private static readonly float MaxSpeed = 7.5f;
    private static readonly float Acceleration = 3f;
    private static readonly float BreakAcceleration = 6f;
    private static readonly float ChangeDirectionAcceleration = 7.5f;
    private static readonly int UpdateFrequency = 5;

    public bool upPressed;
    public bool downPressed;
    public bool leftPressed;
    public bool rightPressed;
    
    private float xVelocity;
    private float yVelocity;

    private MouseHook _mouseHook;
    private Thread _mouseThread;
    
    public MouseController()
    {
        _mouseHook = new MouseHook();
        _mouseThread = new Thread(MouseControlLoop);
        _mouseThread.Start();
    }

    public void Dispose()
    {
        _mouseThread.Abort();
    }
    
    public void SimulateLeftClick()
    {
        _mouseHook.SimulateLeftButtonClick();
    }
    
    public void SimulateRightClick()
    {
        _mouseHook.SimulateRightButtonClick();
    }

    private void MouseControlLoop()
    {
        while (true)
        {
            var deltaT = UpdateFrequency / 1000f;
            // Get input
            float hInput = 0;
            float vInput = 0;
            if (upPressed)
            {
                vInput -= 1;
            }
            if (downPressed)
            {
                vInput += 1;
            }
            if (leftPressed)
            {
                hInput -= 1;
            }
            if (rightPressed)
            {
                hInput += 1;
            }
            // Calculate target speed
            float targetXSpeed = hInput * MaxSpeed;
            float targetYSpeed = vInput * MaxSpeed;
            var (targetXDir, targetYDir) = Normalize(targetXSpeed, targetYSpeed);
            // Choose acceleration
            var acceleration = Acceleration;
            if (targetXDir == 0 && targetYDir == 0)
            {
                acceleration = BreakAcceleration;
            }
            else if (Dot(xVelocity, yVelocity, targetXDir, targetYDir) < 0)
            {
                acceleration = ChangeDirectionAcceleration;
            }
            // Lerp current velocity towards target velocity
            xVelocity = Lerp(xVelocity, targetXDir * MaxSpeed, deltaT * acceleration);
            yVelocity = Lerp(yVelocity, targetYDir * MaxSpeed, deltaT * acceleration);
            // Move mouse
            var iX = (int)Math.Round(xVelocity);
            var iY = (int)Math.Round(yVelocity);
            if (iX != 0 || iY != 0)
            {
                _mouseHook.MoveCursorBy(iX, iY);
            }
            Thread.Sleep(UpdateFrequency);
        }
    }
    
    private static float Dot(float x1, float y1, float x2, float y2)
    {
        return x1 * x2 + y1 * y2;
    }
    
    private static (float,float) Normalize(float x, float y)
    {
        if (x == 0 && y == 0) return (0, 0);
        var magnitude = (float)Math.Sqrt(x * x + y * y);
        return (x / magnitude, y / magnitude);
    }
    
    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}