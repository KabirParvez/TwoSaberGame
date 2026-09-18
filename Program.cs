using Raylib_cs;
using System.Numerics;
using System.Collections.Generic;
using System.IO;

Raylib.InitWindow(1280, 720, "Two Saber");
Raylib.SetTargetFPS(60);

TwoMouseInput twoMice = new TwoMouseInput();

IntPtr windowHandle;

unsafe
{
    windowHandle = (IntPtr)Raylib.GetWindowHandle();
}

twoMice.Initialize(windowHandle);

Raylib.InitAudioDevice();
ProceduralAudio audio = new ProceduralAudio(Raylib.IsAudioDeviceReady());

// Camera
Camera3D camera = new Camera3D
{
    Position = new Vector3(0, 2, 8),
    Target = new Vector3(0, 1, 0),
    Up = new Vector3(0, 1, 0),
    FovY = 60,
    Projection = CameraProjection.Perspective
};

Vector2 saber1Position = new Vector2(400, 360);
Vector2 saber2Position = new Vector2(880, 360);
Vector2 saber1TargetPosition = saber1Position;
Vector2 saber2TargetPosition = saber2Position;

    List<GameObject> objects = new List<GameObject>();
    List<SlicedPiece> slicedPieces = new List<SlicedPiece>();
    List<DebrisParticle> debris = new List<DebrisParticle>();
    List<TrailPoint> blueTrail = new List<TrailPoint>();
    List<TrailPoint> redTrail = new List<TrailPoint>();
    List<HitFlash> hitFlashes = new List<HitFlash>();
    List<SliceMark> sliceMarks = new List<SliceMark>();
    List<PatternType> recentPatterns = new List<PatternType>();
    Random random = new Random();
    
    float spawnTimer = 0f;
    float elapsedTime = 0f;
    int formationNumber = 0;
    int score = 0;
    int misses = 0;
    int combo = 0;
    int bestCombo = 0;
    GameState gameState = GameState.AssigningBlue;
    float missFeedbackTimer = 0f;
    float shakeTimer = 0f;
    float shakeStrength = 0f;
    string comboMessage = string.Empty;
    float comboMessageTimer = 0f;
    Vector3 blueSwingDirection = Vector3.UnitX;
    Vector3 redSwingDirection = Vector3.UnitX;
    float blueSwingSpeed = 0f;
    float redSwingSpeed = 0f;
    float blueSaberAngle = MathF.PI / 2f;
    float redSaberAngle = MathF.PI / 2f;
    int blueMouseIndex = -1;
    int redMouseIndex = -1;
    int lives = 3;
    float readyTimer = 0f;
    float wrongHitTimer = 0f;
    Vector2 blueSaberVelocity = Vector2.Zero;
    Vector2 redSaberVelocity = Vector2.Zero;
    
    while (!Raylib.WindowShouldClose())
    {
        float deltaTime = Raylib.GetFrameTime();

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            break;
        }
        
        if (gameState == GameState.AssigningBlue)
        {
            bool mouse0Clicked = twoMice.GetLeftClick(0);
            bool mouse1Clicked = twoMice.GetLeftClick(1);
            if (mouse0Clicked)
            {
                blueMouseIndex = 0;
                gameState = GameState.AssigningRed;
            }
            else if (mouse1Clicked)
            {
                blueMouseIndex = 1;
                gameState = GameState.AssigningRed;
            }
        }

        if (gameState == GameState.AssigningRed)
        {
            bool mouse0Clicked = twoMice.GetLeftClick(0);
            bool mouse1Clicked = twoMice.GetLeftClick(1);
            int otherMouseIndex = blueMouseIndex == 0 ? 1 : 0;
            if ((otherMouseIndex == 0 && mouse0Clicked) || (otherMouseIndex == 1 && mouse1Clicked))
            {
                redMouseIndex = otherMouseIndex;
                readyTimer = 1f;
                gameState = GameState.Ready;
                audio.PlayStart();
            }
        }

        if (gameState == GameState.Ready)
        {
            readyTimer -= deltaTime;
            if (readyTimer <= 0f)
            {
                gameState = GameState.Playing;
            }
        }

        if (gameState == GameState.GameOver && Raylib.IsKeyPressed(KeyboardKey.R))
        {
            ResetGame();
            gameState = GameState.AssigningBlue;
        }
        
        Vector2 rawMouse0 = twoMice.GetDelta(0);
        Vector2 rawMouse1 = twoMice.GetDelta(1);
        Vector2 mouse1 = blueMouseIndex == 0 ? rawMouse0 : rawMouse1;
        Vector2 mouse2 = redMouseIndex == 0 ? rawMouse0 : rawMouse1;
        
        saber1TargetPosition += mouse1;
        saber2TargetPosition += mouse2;
        
        saber1TargetPosition.X = Math.Clamp(saber1TargetPosition.X, 50, 1230);
        saber1TargetPosition.Y = Math.Clamp(saber1TargetPosition.Y, 50, 670);
        saber2TargetPosition.X = Math.Clamp(saber2TargetPosition.X, 50, 1230);
        saber2TargetPosition.Y = Math.Clamp(saber2TargetPosition.Y, 50, 670);

        UpdateSaberMotion(ref saber1Position, saber1TargetPosition, ref blueSaberVelocity, deltaTime);
        UpdateSaberMotion(ref saber2Position, saber2TargetPosition, ref redSaberVelocity, deltaTime);
        
        float saber1X = (saber1Position.X - 640) / 200f;
        float saber1Y = -(saber1Position.Y - 360) / 200f;
        float saber2X = (saber2Position.X - 640) / 200f;
        float saber2Y = -(saber2Position.Y - 360) / 200f;
        
        Vector3 saber1 = new Vector3(saber1X, saber1Y + 1f, 5.5f);
        Vector3 saber2 = new Vector3(saber2X, saber2Y + 1f, 5.5f);

        UpdateSwing(mouse1, deltaTime, ref blueSwingDirection, ref blueSwingSpeed, ref blueSaberAngle);
        UpdateSwing(mouse2, deltaTime, ref redSwingDirection, ref redSwingSpeed, ref redSaberAngle);
        TrackTrail(blueTrail, saber1, blueSwingSpeed, deltaTime);
        TrackTrail(redTrail, saber2, redSwingSpeed, deltaTime);
        audio.UpdateSabers(gameState == GameState.Playing, blueSwingSpeed, redSwingSpeed, blueSwingDirection, redSwingDirection, deltaTime);
        
        if (gameState == GameState.Playing)
        {
            elapsedTime += deltaTime;
            float objectSpeed = Math.Min(11.5f, 6f + elapsedTime * 0.16f);
            float spawnInterval = Math.Max(0.85f, 1.5f - elapsedTime * 0.006f);

            spawnTimer -= deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = SpawnFormation(spawnInterval);
            }

            foreach (GameObject obj in objects)
            {
                obj.Position.Z += objectSpeed * deltaTime;
                obj.Rotation += deltaTime * 55f;
                obj.WrongHitCooldown = Math.Max(0f, obj.WrongHitCooldown - deltaTime);
            }

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                GameObject obj = objects[i];
                Vector3 matchingSaber = obj.IsBlue ? saber1 : saber2;
                Vector3 wrongSaber = obj.IsBlue ? saber2 : saber1;
                Vector3 matchingBladeEnd = obj.IsBlue ? GetBladeEnd(saber1, blueSaberAngle) : GetBladeEnd(saber2, redSaberAngle);
                Vector3 wrongBladeEnd = obj.IsBlue ? GetBladeEnd(saber2, redSaberAngle) : GetBladeEnd(saber1, blueSaberAngle);
                Vector3 swingDirection = obj.IsBlue ? blueSwingDirection : redSwingDirection;
                float swingSpeed = obj.IsBlue ? blueSwingSpeed : redSwingSpeed;
                float wrongDistance = DistanceToSegment(obj.Position, wrongSaber, wrongBladeEnd);

                if (wrongDistance >= 1.5f)
                {
                    obj.WrongHitActive = false;
                }

                if (!obj.WrongHitActive && obj.WrongHitCooldown <= 0f && wrongDistance < 1.5f)
                {
                    obj.WrongHitCooldown = 0.35f;
                    obj.WrongHitActive = true;
                    LoseLife(true);
                    continue;
                }

                if (DistanceToSegment(obj.Position, matchingSaber, matchingBladeEnd) < 1.25f)
                {
                    float slicePower = Math.Clamp(swingSpeed / 850f, 0f, 1f);
                    SliceObject(obj, swingDirection, slicePower);
                    objects.RemoveAt(i);
                    score += 100;
                    combo++;
                    bestCombo = Math.Max(bestCombo, combo);
                    hitFlashes.Add(new HitFlash
                    {
                        Position = obj.Position,
                        IsBlue = obj.IsBlue,
                        Lifetime = 0.18f,
                        Strength = 0.7f + slicePower * 0.9f
                    });
                    shakeTimer = 0.1f;
                    shakeStrength = 0.06f + slicePower * 0.06f;
                    audio.PlayHit(obj.IsBlue, slicePower);
                    if (IsComboMilestone(combo))
                    {
                        comboMessage = $"{combo} COMBO!";
                        comboMessageTimer = 0.9f;
                        audio.PlayCombo();
                    }
                    continue;
                }

                if (obj.Position.Z > 7f)
                {
                    objects.RemoveAt(i);
                    LoseLife(false);
                    combo = 0;
                    missFeedbackTimer = 0.45f;
                    shakeTimer = 0.08f;
                    shakeStrength = 0.035f;
                    audio.PlayMiss();
                    if (lives <= 0)
                    {
                        gameState = GameState.GameOver;
                        audio.PlayGameOver();
                        break;
                    }
                }
            }
        }
        
        UpdateEffects(deltaTime);
        missFeedbackTimer = Math.Max(0f, missFeedbackTimer - deltaTime);
        wrongHitTimer = Math.Max(0f, wrongHitTimer - deltaTime);
        comboMessageTimer = Math.Max(0f, comboMessageTimer - deltaTime);
        shakeTimer = Math.Max(0f, shakeTimer - deltaTime);
        audio.UpdateMusic(elapsedTime);

        float shakeX = shakeTimer > 0f ? (random.NextSingle() * 2f - 1f) * shakeStrength : 0f;
        float shakeY = shakeTimer > 0f ? (random.NextSingle() * 2f - 1f) * shakeStrength : 0f;
        camera.Position = new Vector3(shakeX, 2f + shakeY, 8f);
        camera.Target = new Vector3(shakeX, 1f + shakeY, 0f);
        
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Raylib.BeginMode3D(camera);
        
        Raylib.DrawPlane(new Vector3(0, 0, 0), new Vector2(30, 30), Color.DarkGray);
        DrawTrail(blueTrail, true);
        DrawTrail(redTrail, false);
        DrawSaber(saber1, Color.Blue, blueSaberAngle, blueSwingSpeed);
        DrawSaber(saber2, Color.Red, redSaberAngle, redSwingSpeed);
        
        foreach (GameObject obj in objects)
        {
            DrawTarget(obj);
        }

        foreach (SlicedPiece piece in slicedPieces)
        {
            Raylib.DrawCube(piece.Position, 0.48f, 0.9f, 0.9f, piece.IsBlue ? Color.Blue : Color.Red);
        }

        foreach (DebrisParticle particle in debris)
        {
            Raylib.DrawSphere(particle.Position, particle.Size, particle.IsBlue ? Color.Blue : Color.Red);
        }

        foreach (HitFlash flash in hitFlashes)
        {
            float size = (0.7f + (0.18f - flash.Lifetime) * 3f) * flash.Strength;
            Raylib.DrawSphere(flash.Position, size, flash.IsBlue ? new Color((byte)80, (byte)180, (byte)255, (byte)130) : new Color((byte)255, (byte)100, (byte)100, (byte)130));
        }

        foreach (SliceMark mark in sliceMarks)
        {
            float length = 0.75f * mark.Strength;
            Vector3 start = mark.Position - mark.Direction * length;
            Vector3 end = mark.Position + mark.Direction * length;
            Color markColor = mark.IsBlue
                ? new Color((byte)130, (byte)220, (byte)255, (byte)220)
                : new Color((byte)255, (byte)150, (byte)150, (byte)220);
            Raylib.DrawLine3D(start, end, markColor);
            Raylib.DrawLine3D(start + Vector3.UnitZ * 0.02f, end + Vector3.UnitZ * 0.02f, Color.White);
        }
        
        Raylib.DrawGrid(30, 1);
        Raylib.EndMode3D();
        
        Raylib.DrawText($"SCORE: {score}", 1030, 20, 25, Color.White);
        Color lifeColor = lives == 3 ? Color.Green : (lives == 2 ? Color.Yellow : Color.Red);
        Raylib.DrawText($"LIVES: {LivesDisplay()}", 1010, 50, 25, lifeColor);
        Raylib.DrawText($"COMBO: {combo}", 1030, 80, 25, Color.Yellow);

        if (missFeedbackTimer > 0f)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)180, (byte)0, (byte)0, (byte)35));
            Raylib.DrawText("MISS!", 570, 300, 42, Color.Red);
        }

        if (comboMessageTimer > 0f)
        {
            Raylib.DrawText(comboMessage, 510, 245, 42, Color.Yellow);
        }
        
        if (gameState == GameState.AssigningBlue)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)170));
            DrawStartSabers();
            Raylib.DrawText("TWO SABER", 470, 105, 54, Color.White);
            Raylib.DrawText("ASSIGN YOUR SABERS", 390, 190, 54, Color.White);
            Raylib.DrawText("Left Click with the mouse you want to control", 320, 305, 25, Color.LightGray);
            Raylib.DrawText("the BLUE SABER", 500, 340, 30, Color.Blue);
        }

        if (gameState == GameState.AssigningRed)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)170));
            DrawStartSabers();
            Raylib.DrawText("TWO SABER", 470, 105, 54, Color.White);
            Raylib.DrawText("ASSIGN YOUR SABERS", 390, 190, 54, Color.White);
            Raylib.DrawText("Now Left Click with the other mouse for", 350, 305, 25, Color.LightGray);
            Raylib.DrawText("the RED SABER", 505, 340, 30, Color.Red);
        }

        if (gameState == GameState.Ready)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)160));
            Raylib.DrawText("READY", 525, 300, 64, Color.Green);
        }

        if (wrongHitTimer > 0f)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)255, (byte)120, (byte)0, (byte)45));
            Raylib.DrawText("WRONG SABER!", 500, 300, 38, Color.Orange);
        }

        if (gameState == GameState.GameOver)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)150));
            Raylib.DrawText("GAME OVER", 455, 245, 64, Color.Red);
            Raylib.DrawText($"FINAL SCORE: {score}", 500, 330, 28, Color.White);
            Raylib.DrawText($"FINAL COMBO: {bestCombo}", 500, 370, 28, Color.Yellow);
            Raylib.DrawText($"LIVES: {LivesDisplay()}", 500, 410, 28, Color.Red);
            Raylib.DrawText("Press R to Restart", 500, 475, 28, Color.White);
            Raylib.DrawText("Press Escape to Quit", 500, 515, 24, Color.LightGray);
        }
        
        Raylib.EndDrawing();
    }
    
    audio.Dispose();
    Raylib.CloseAudioDevice();
    Raylib.CloseWindow();

    void ResetGame()
    {
        objects.Clear();
        slicedPieces.Clear();
        debris.Clear();
        hitFlashes.Clear();
        sliceMarks.Clear();
        blueTrail.Clear();
        redTrail.Clear();
        recentPatterns.Clear();
        blueMouseIndex = -1;
        redMouseIndex = -1;
        saber1Position = new Vector2(400, 360);
        saber2Position = new Vector2(880, 360);
        saber1TargetPosition = saber1Position;
        saber2TargetPosition = saber2Position;
        blueSaberVelocity = Vector2.Zero;
        redSaberVelocity = Vector2.Zero;
        blueSaberAngle = MathF.PI / 2f;
        redSaberAngle = MathF.PI / 2f;
        spawnTimer = 0f;
        elapsedTime = 0f;
        formationNumber = 0;
        score = 0;
        misses = 0;
        lives = 3;
        combo = 0;
        bestCombo = 0;
        missFeedbackTimer = 0f;
        shakeTimer = 0f;
        comboMessage = string.Empty;
        comboMessageTimer = 0f;
        wrongHitTimer = 0f;
    }

    void UpdateSaberMotion(ref Vector2 position, Vector2 target, ref Vector2 velocity, float deltaTime)
    {
        Vector2 error = target - position;
        Vector2 springForce = error * 115f;
        velocity += springForce * deltaTime;
        velocity *= MathF.Exp(-13f * deltaTime);

        float maxVelocity = 900f;
        if (velocity.LengthSquared() > maxVelocity * maxVelocity)
        {
            velocity = Vector2.Normalize(velocity) * maxVelocity;
        }

        position += velocity * deltaTime;
        position = Vector2.Clamp(position, new Vector2(50f, 50f), new Vector2(1230f, 670f));
    }

    Vector3 GetBladeEnd(Vector3 handle, float angle)
    {
        Vector3 bladeDirection = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
        return handle + bladeDirection * 2.7f;
    }

    float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared < 0.0001f)
        {
            return Vector3.Distance(point, start);
        }

        float projection = Vector3.Dot(point - start, segment) / lengthSquared;
        projection = Math.Clamp(projection, 0f, 1f);
        return Vector3.Distance(point, start + segment * projection);
    }

    void LoseLife(bool wrongColor)
    {
        lives = Math.Max(0, lives - 1);
        misses++;
        combo = 0;
        missFeedbackTimer = 0.45f;
        wrongHitTimer = wrongColor ? 0.55f : 0f;
        shakeTimer = wrongColor ? 0.12f : 0.08f;
        shakeStrength = wrongColor ? 0.05f : 0.035f;
        if (wrongColor)
        {
            audio.PlayWrongHit();
        }
        else
        {
            audio.PlayMiss();
        }

        if (lives == 0)
        {
            gameState = GameState.GameOver;
            audio.PlayGameOver();
        }
    }

    string LivesDisplay()
    {
        return (lives >= 1 ? "♥ " : "- ") +
            (lives >= 2 ? "♥ " : "- ") +
            (lives >= 3 ? "♥" : "-");
    }

    void DrawStartSabers()
    {
        Raylib.DrawRectangle(405, 390, 18, 115, Color.Blue);
        Raylib.DrawRectangle(857, 390, 18, 115, Color.Red);
        Raylib.DrawRectangle(400, 385, 28, 125, new Color((byte)30, (byte)120, (byte)255, (byte)90));
        Raylib.DrawRectangle(852, 385, 28, 125, new Color((byte)255, (byte)30, (byte)30, (byte)90));
        Raylib.DrawText("BLUE", 390, 525, 22, Color.Blue);
        Raylib.DrawText("RED", 850, 525, 22, Color.Red);
    }

    void UpdateSwing(Vector2 mouseDelta, float deltaTime, ref Vector3 direction, ref float speed, ref float angle)
    {
        float distance = mouseDelta.Length();
        if (distance > 0.01f && deltaTime > 0f)
        {
            Vector3 movement = new Vector3(mouseDelta.X, -mouseDelta.Y, 0f);
            Vector3 movementDirection = Vector3.Normalize(movement);
            direction = Vector3.Normalize(Vector3.Lerp(direction, movementDirection, 0.45f));
            float measuredSpeed = distance / deltaTime;
            float speedBlend = 1f - MathF.Exp(-12f * deltaTime);
            speed += (measuredSpeed - speed) * speedBlend;
            float targetAngle = MathF.Atan2(movementDirection.Y, movementDirection.X);
            angle = LerpAngle(angle, targetAngle, 1f - MathF.Exp(-16f * deltaTime));
        }
        else
        {
            float stopBlend = 1f - MathF.Exp(-9f * deltaTime);
            speed += (0f - speed) * stopBlend;
            angle = LerpAngle(angle, MathF.PI / 2f, 1f - MathF.Exp(-5f * deltaTime));
        }
    }

    float LerpAngle(float current, float target, float amount)
    {
        float difference = MathF.Atan2(MathF.Sin(target - current), MathF.Cos(target - current));
        return current + difference * amount;
    }

    bool IsComboMilestone(int value)
    {
        return value == 5 || value == 10 || value == 20 || value % 10 == 0;
    }

    float SpawnFormation(float baseInterval)
    {
        float difficulty = Math.Clamp(elapsedTime / 180f, 0f, 1f);
        PatternType pattern = ChoosePattern(difficulty);
        float z = -24f;
        float sequenceSpacing = 3.6f - difficulty * 0.7f;

        switch (pattern)
        {
            case PatternType.BlueSingle:
                AddTarget(Lane(-1), true, z);
                break;
            case PatternType.RedSingle:
                AddTarget(Lane(1), false, z);
                break;
            case PatternType.CenterSingle:
                AddTarget(Lane(0), random.Next(2) == 0, z);
                break;
            case PatternType.BlueRedPair:
                AddTarget(Lane(-1), true, z);
                AddTarget(Lane(1), false, z);
                break;
            case PatternType.RedBluePair:
                AddTarget(Lane(-1), false, z);
                AddTarget(Lane(1), true, z);
                break;
            case PatternType.BlueSplitPair:
                AddTarget(Lane(-1), true, z);
                AddTarget(Lane(1), true, z);
                break;
            case PatternType.RedSplitPair:
                AddTarget(Lane(-1), false, z);
                AddTarget(Lane(1), false, z);
                break;
            case PatternType.BlueRedBlue:
                AddSequence(new[] { true, false, true }, new[] { -2, 0, 2 }, z, sequenceSpacing);
                break;
            case PatternType.RedBlueRed:
                AddSequence(new[] { false, true, false }, new[] { 2, 0, -2 }, z, sequenceSpacing);
                break;
            case PatternType.BlueBlueRed:
                AddSequence(new[] { true, true, false }, new[] { -1, 0, 1 }, z, sequenceSpacing);
                break;
            case PatternType.RedRedBlue:
                AddSequence(new[] { false, false, true }, new[] { 1, 0, -1 }, z, sequenceSpacing);
                break;
            case PatternType.AlternatingFour:
                AddSequence(new[] { true, false, true, false }, new[] { -2, -1, 1, 2 }, z, sequenceSpacing);
                break;
            case PatternType.ReverseAlternatingFour:
                AddSequence(new[] { false, true, false, true }, new[] { 2, 1, -1, -2 }, z, sequenceSpacing);
                break;
        }

        float rhythmGap = random.NextSingle() < 0.16f ? 1.25f : (random.NextSingle() < 0.3f ? 0.92f : 1f);
        formationNumber++;
        return baseInterval * rhythmGap;
    }

    PatternType ChoosePattern(float difficulty)
    {
        PatternType[] simple = { PatternType.BlueSingle, PatternType.RedSingle, PatternType.CenterSingle };
        PatternType[] medium = { PatternType.BlueRedPair, PatternType.RedBluePair, PatternType.BlueSplitPair, PatternType.RedSplitPair, PatternType.BlueBlueRed, PatternType.RedRedBlue };
        PatternType[] complex = { PatternType.BlueRedBlue, PatternType.RedBlueRed, PatternType.AlternatingFour, PatternType.ReverseAlternatingFour };
        PatternType[] pool;

        float roll = random.NextSingle();
        if (difficulty < 0.22f || (difficulty < 0.5f && roll < 0.55f))
        {
            pool = simple;
        }
        else if (difficulty < 0.75f || roll < 0.8f)
        {
            pool = medium;
        }
        else
        {
            pool = complex;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            PatternType candidate = pool[random.Next(pool.Length)];
            if (IsPatternReachable(candidate, difficulty) && !recentPatterns.Contains(candidate))
            {
                recentPatterns.Add(candidate);
                if (recentPatterns.Count > 3)
                    recentPatterns.RemoveAt(0);
                return candidate;
            }
        }

        PatternType fallback = IsPatternReachable(pool[0], difficulty) ? pool[0] : PatternType.CenterSingle;
        recentPatterns.Add(fallback);
        if (recentPatterns.Count > 3)
            recentPatterns.RemoveAt(0);
        return fallback;
    }

    bool IsPatternReachable(PatternType pattern, float difficulty)
    {
        bool sameSaberPair = pattern == PatternType.BlueSplitPair || pattern == PatternType.RedSplitPair;
        bool longSequence = pattern == PatternType.AlternatingFour || pattern == PatternType.ReverseAlternatingFour;
        float minimumSequenceGap = 2.8f - difficulty * 0.35f;
        return (!sameSaberPair || difficulty > 0.12f) && (!longSequence || minimumSequenceGap >= 2.45f);
    }

    Vector3 Lane(int lane)
    {
        float[] lanes = { -2.1f, -1.05f, 0f, 1.05f, 2.1f };
        return new Vector3(lanes[Math.Clamp(lane + 2, 0, 4)], 1.35f, -24f);
    }

    void AddSequence(bool[] colors, int[] lanes, float z, float spacing)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            Vector3 position = Lane(lanes[i]);
            AddTarget(new Vector3(position.X, position.Y, z - i * spacing), colors[i], position.Z);
        }
    }

    void AddTarget(Vector3 position, bool isBlue, float z)
    {
        objects.Add(new GameObject
        {
            Position = new Vector3(position.X, position.Y, z),
            IsBlue = isBlue,
            Rotation = random.NextSingle() * 360f,
            WrongHitCooldown = 0f,
            WrongHitActive = false
        });
    }

    void TrackTrail(List<TrailPoint> trail, Vector3 position, float speed, float deltaTime)
    {
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            trail[i].Lifetime -= deltaTime;
            if (trail[i].Lifetime <= 0f)
            {
                trail.RemoveAt(i);
            }
        }

        if (trail.Count == 0 || Vector3.DistanceSquared(trail[^1].Position, position) > 0.0001f)
        {
            float lifetime = 0.12f + Math.Clamp(speed / 1800f, 0f, 1f) * 0.28f;
            trail.Add(new TrailPoint { Position = position, Lifetime = lifetime, MaxLifetime = lifetime });
        }

        while (trail.Count > 24)
        {
            trail.RemoveAt(0);
        }
    }

    void DrawTrail(List<TrailPoint> trail, bool isBlue)
    {
        Color color = isBlue ? Color.Blue : Color.Red;

        for (int i = 1; i < trail.Count; i++)
        {
            TrailPoint previous = trail[i - 1];
            TrailPoint current = trail[i];
            byte alpha = (byte)Math.Clamp((int)(current.Lifetime / current.MaxLifetime * 170f), 0, 170);
            Color trailColor = isBlue
                ? new Color((byte)40, (byte)150, (byte)255, alpha)
                : new Color((byte)255, (byte)60, (byte)60, alpha);

            Raylib.DrawLine3D(previous.Position, current.Position, trailColor);
        }

        if (trail.Count > 0)
        {
            TrailPoint newest = trail[^1];
            Raylib.DrawSphere(newest.Position, 0.08f, color);
        }
    }

    void DrawSaber(Vector3 position, Color color, float angle, float speed)
    {
        float intensity = Math.Clamp(speed / 1100f, 0f, 1f);
        Vector3 direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
        Vector3 start = position;
        Vector3 end = position + direction * 2.7f;
        Color outerColor = color == Color.Blue
            ? new Color((byte)30, (byte)(120 + intensity * 80), (byte)255, (byte)(80 + intensity * 60))
            : new Color((byte)255, (byte)(30 + intensity * 80), (byte)(30 + intensity * 40), (byte)(80 + intensity * 60));

        Raylib.DrawCylinderEx(start, end, 0.17f + intensity * 0.05f, 0.17f + intensity * 0.05f, 10, outerColor);
        Raylib.DrawCylinderEx(start, end, 0.095f + intensity * 0.02f, 0.095f + intensity * 0.02f, 10, color);
        Raylib.DrawCylinderEx(start, end, 0.035f, 0.035f, 8, Color.White);
    }

    void DrawTarget(GameObject obj)
    {
        Color color = obj.IsBlue ? Color.Blue : Color.Red;
        Color glowColor = obj.IsBlue
            ? new Color((byte)30, (byte)120, (byte)255, (byte)75)
            : new Color((byte)255, (byte)30, (byte)30, (byte)75);

        Raylib.DrawCube(obj.Position, 1f, 1f, 1f, color);
        Raylib.DrawCubeWires(obj.Position, 1.16f, 1.16f, 1.16f, Color.White);
        Raylib.DrawCircle3D(obj.Position, 0.78f, Vector3.UnitZ, obj.Rotation, glowColor);
        Raylib.DrawCircle3D(obj.Position, 0.78f, Vector3.UnitX, -obj.Rotation * 0.7f, glowColor);
    }
    
    void SliceObject(GameObject obj, Vector3 cutDirection, float slicePower)
    {
        cutDirection.Z = 0f;
        if (cutDirection.LengthSquared() < 0.001f)
        {
            cutDirection = Vector3.UnitX;
        }
        else
        {
            cutDirection = Vector3.Normalize(cutDirection);
        }

        Vector3 separationDirection = Vector3.Normalize(new Vector3(-cutDirection.Y, cutDirection.X, 0f));
        float separation = 0.2f + slicePower * 0.25f;
        float impulse = 0.7f + slicePower * 1.3f;

        sliceMarks.Add(new SliceMark
        {
            Position = obj.Position,
            Direction = cutDirection,
            IsBlue = obj.IsBlue,
            Strength = 0.8f + slicePower * 0.8f,
            Lifetime = 0.2f
        });
        
        slicedPieces.Add(new SlicedPiece
        {
            Position = obj.Position - separationDirection * separation,
            Velocity = -separationDirection * impulse + cutDirection * 0.2f,
            IsBlue = obj.IsBlue
        });
        slicedPieces.Add(new SlicedPiece
        {
            Position = obj.Position + separationDirection * separation,
            Velocity = separationDirection * impulse + cutDirection * 0.2f,
            IsBlue = obj.IsBlue
        });
        
        int debrisCount = 8 + (int)(slicePower * 12f);
        for (int i = 0; i < debrisCount; i++)
        {
            debris.Add(new DebrisParticle
            {
                Position = obj.Position,
                Velocity = new Vector3(
                    (random.NextSingle() * 2f - 1f) * (0.8f + slicePower),
                    (random.NextSingle() * 2f - 1f) * (0.8f + slicePower),
                    (random.NextSingle() * 2f - 1f) * (0.8f + slicePower)),
                Size = 0.04f + random.NextSingle() * (0.04f + slicePower * 0.03f),
                Lifetime = 0.45f + slicePower * 0.2f,
                IsBlue = obj.IsBlue
            });
        }
    }
    
    void UpdateEffects(float deltaTime)
    {
        for (int i = slicedPieces.Count - 1; i >= 0; i--)
        {
            SlicedPiece piece = slicedPieces[i];
            piece.Position += piece.Velocity * deltaTime;
            piece.Velocity.Y -= 2.5f * deltaTime;
            piece.Lifetime -= deltaTime;
            
            if (piece.Lifetime <= 0f)
            {
                slicedPieces.RemoveAt(i);
            }
        }
        
        for (int i = debris.Count - 1; i >= 0; i--)
        {
            DebrisParticle particle = debris[i];
            particle.Position += particle.Velocity * deltaTime;
            particle.Velocity.Y -= 3f * deltaTime;
            particle.Lifetime -= deltaTime;
            
            if (particle.Lifetime <= 0f)
            {
                debris.RemoveAt(i);
            }
        }

        for (int i = hitFlashes.Count - 1; i >= 0; i--)
        {
            hitFlashes[i].Lifetime -= deltaTime;
            if (hitFlashes[i].Lifetime <= 0f)
            {
                hitFlashes.RemoveAt(i);
            }
        }

        for (int i = sliceMarks.Count - 1; i >= 0; i--)
        {
            sliceMarks[i].Lifetime -= deltaTime;
            if (sliceMarks[i].Lifetime <= 0f)
            {
                sliceMarks.RemoveAt(i);
            }
        }
    }
    
    public class GameObject
    {
        public Vector3 Position;
        public bool IsBlue;
        public float Rotation;
        public float WrongHitCooldown;
        public bool WrongHitActive;
    }

    public class TrailPoint
    {
        public Vector3 Position;
        public float Lifetime;
        public float MaxLifetime;
    }

    public class HitFlash
    {
        public Vector3 Position;
        public bool IsBlue;
        public float Lifetime;
        public float Strength;
    }

    public class SliceMark
    {
        public Vector3 Position;
        public Vector3 Direction;
        public bool IsBlue;
        public float Strength;
        public float Lifetime;
    }
    
    public class SlicedPiece
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public bool IsBlue;
        public float Lifetime = 0.8f;
    }
    
    public class DebrisParticle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Size;
        public float Lifetime;
        public bool IsBlue;
    }

    public enum GameState
    {
        AssigningBlue,
        AssigningRed,
        Ready,
        Playing,
        GameOver
    }

    public enum PatternType
    {
        BlueSingle,
        RedSingle,
        CenterSingle,
        BlueRedPair,
        RedBluePair,
        BlueSplitPair,
        RedSplitPair,
        BlueRedBlue,
        RedBlueRed,
        BlueBlueRed,
        RedRedBlue,
        AlternatingFour,
        ReverseAlternatingFour
    }

    public class ProceduralAudio
    {
        private readonly bool _enabled;
        private Sound _blueHit;
        private Sound _redHit;
        private Sound _miss;
        private Sound _combo;
        private Sound _gameOver;
        private Sound _start;
        private Sound _wrongHit;
        private Sound _blueHum;
        private Sound _redHum;
        private Sound _blueWhoosh;
        private Sound _redWhoosh;
        private Music _music;
        private bool _musicLoaded;
        private float _blueWhooshCooldown;
        private float _redWhooshCooldown;

        public ProceduralAudio(bool enabled)
        {
            _enabled = enabled;
            if (!_enabled)
            {
                return;
            }

            _blueHit = CreateTone(520f, 0.11f, 0.22f, 100f);
            _redHit = CreateTone(680f, 0.11f, 0.22f, 120f);
            _miss = CreateTone(130f, 0.18f, 0.25f, -45f);
            _combo = CreateTone(900f, 0.24f, 0.2f, 260f);
            _gameOver = CreateTone(180f, 0.55f, 0.25f, -80f);
            _start = CreateTone(440f, 0.3f, 0.2f, 180f);
            _wrongHit = CreateTone(85f, 0.2f, 0.28f, -35f);
            _blueHum = CreateTone(115f, 1.2f, 0.12f, 4f);
            _redHum = CreateTone(128f, 1.2f, 0.12f, 4f);
            _blueWhoosh = CreateTone(260f, 0.18f, 0.2f, 420f);
            _redWhoosh = CreateTone(300f, 0.18f, 0.2f, 480f);

            string musicPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "music.ogg");
            if (!File.Exists(musicPath))
            {
                musicPath = Path.Combine(AppContext.BaseDirectory, "assets", "music.ogg");
            }

            if (File.Exists(musicPath))
            {
                _music = Raylib.LoadMusicStream(musicPath);
                _musicLoaded = Raylib.IsMusicValid(_music);
                if (_musicLoaded)
                {
                    Raylib.SetMusicVolume(_music, 0.12f);
                    Raylib.PlayMusicStream(_music);
                }
            }
        }

        public void PlayStart() => Play(_start);

        public void PlayHit(bool isBlue, float intensity = 0f)
        {
            Sound sound = isBlue ? _blueHit : _redHit;
            SetSoundProfile(sound, 0.75f + intensity * 0.25f, 0.95f + intensity * 0.15f);
            Play(sound);
        }

        public void PlayWrongHit() => Play(_wrongHit);

        public void PlayMiss() => Play(_miss);

        public void PlayCombo() => Play(_combo);

        public void PlayGameOver() => Play(_gameOver);

        public void UpdateSabers(bool active, float blueSpeed, float redSpeed, Vector3 blueDirection, Vector3 redDirection, float deltaTime)
        {
            if (!_enabled)
            {
                return;
            }

            _blueWhooshCooldown = Math.Max(0f, _blueWhooshCooldown - deltaTime);
            _redWhooshCooldown = Math.Max(0f, _redWhooshCooldown - deltaTime);

            if (!active)
            {
                StopHum(_blueHum);
                StopHum(_redHum);
                return;
            }

            UpdateHum(_blueHum, blueSpeed);
            UpdateHum(_redHum, redSpeed);
            TryWhoosh(_blueWhoosh, blueSpeed, blueDirection, ref _blueWhooshCooldown);
            TryWhoosh(_redWhoosh, redSpeed, redDirection, ref _redWhooshCooldown);
        }

        public void UpdateMusic(float elapsedTime)
        {
            if (!_enabled || !_musicLoaded)
            {
                return;
            }

            Raylib.UpdateMusicStream(_music);
            float intensity = Math.Min(0.2f, 0.12f + elapsedTime * 0.0005f);
            Raylib.SetMusicVolume(_music, intensity);
        }

        public void Dispose()
        {
            if (!_enabled)
            {
                return;
            }

            Unload(_blueHit);
            Unload(_redHit);
            Unload(_miss);
            Unload(_combo);
            Unload(_gameOver);
            Unload(_start);
            Unload(_wrongHit);
            Unload(_blueHum);
            Unload(_redHum);
            Unload(_blueWhoosh);
            Unload(_redWhoosh);

            if (_musicLoaded)
            {
                Raylib.UnloadMusicStream(_music);
            }
        }

        private static void Play(Sound sound)
        {
            if (Raylib.IsSoundValid(sound))
            {
                Raylib.PlaySound(sound);
            }
        }

        private static void Unload(Sound sound)
        {
            if (Raylib.IsSoundValid(sound))
            {
                Raylib.UnloadSound(sound);
            }
        }

        private static void SetSoundProfile(Sound sound, float volume, float pitch)
        {
            if (Raylib.IsSoundValid(sound))
            {
                Raylib.SetSoundVolume(sound, Math.Clamp(volume, 0.05f, 1f));
                Raylib.SetSoundPitch(sound, Math.Clamp(pitch, 0.5f, 2f));
            }
        }

        private static void StopHum(Sound sound)
        {
            if (Raylib.IsSoundValid(sound) && Raylib.IsSoundPlaying(sound))
            {
                Raylib.StopSound(sound);
            }
        }

        private static void UpdateHum(Sound sound, float speed)
        {
            if (!Raylib.IsSoundValid(sound))
            {
                return;
            }

            float movement = Math.Clamp(speed / 1000f, 0f, 1f);
            Raylib.SetSoundVolume(sound, 0.035f + movement * 0.055f);
            Raylib.SetSoundPitch(sound, 0.96f + movement * 0.12f);
            if (!Raylib.IsSoundPlaying(sound))
            {
                Raylib.PlaySound(sound);
            }
        }

        private static void TryWhoosh(Sound sound, float speed, Vector3 direction, ref float cooldown)
        {
            if (!Raylib.IsSoundValid(sound) || cooldown > 0f || speed < 180f)
            {
                return;
            }

            float intensity = Math.Clamp((speed - 180f) / 900f, 0f, 1f);
            float diagonalBoost = 1f - Math.Abs(direction.X * direction.Y);
            SetSoundProfile(sound, 0.08f + intensity * 0.3f, 0.85f + intensity * 0.35f + diagonalBoost * 0.08f);
            Raylib.PlaySound(sound);
            cooldown = 0.16f - intensity * 0.05f;
        }

        private static Sound CreateTone(float frequency, float duration, float volume, float frequencyChange)
        {
            const int sampleRate = 22050;
            int sampleCount = Math.Max(1, (int)(sampleRate * duration));

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            int dataSize = sampleCount * sizeof(short);

            writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + dataSize);
            writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataSize);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float progress = i / (float)sampleCount;
                float currentFrequency = frequency + frequencyChange * progress;
                float envelope = MathF.Min(1f, progress * 30f) * MathF.Min(1f, (1f - progress) * 8f);
                float sample = MathF.Sin(MathF.Tau * currentFrequency * time) * volume * envelope;
                writer.Write((short)(sample * short.MaxValue));
            }

            Wave wave = Raylib.LoadWaveFromMemory(".wav", stream.ToArray());
            if (!Raylib.IsWaveValid(wave))
            {
                return default;
            }

            Sound sound = Raylib.LoadSoundFromWave(wave);
            Raylib.UnloadWave(wave);
            return sound;
        }
    }

