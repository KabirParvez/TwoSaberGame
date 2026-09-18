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

    List<GameObject> objects = new List<GameObject>();
    List<SlicedPiece> slicedPieces = new List<SlicedPiece>();
    List<DebrisParticle> debris = new List<DebrisParticle>();
    List<TrailPoint> blueTrail = new List<TrailPoint>();
    List<TrailPoint> redTrail = new List<TrailPoint>();
    List<HitFlash> hitFlashes = new List<HitFlash>();
    List<SliceMark> sliceMarks = new List<SliceMark>();
    Random random = new Random();
    
    float spawnTimer = 0f;
    float elapsedTime = 0f;
    int formationNumber = 0;
    int score = 0;
    int misses = 0;
    int combo = 0;
    int bestCombo = 0;
    GameState gameState = GameState.Start;
    float missFeedbackTimer = 0f;
    float shakeTimer = 0f;
    float shakeStrength = 0f;
    string comboMessage = string.Empty;
    float comboMessageTimer = 0f;
    Vector3 blueSwingDirection = Vector3.UnitX;
    Vector3 redSwingDirection = Vector3.UnitX;
    float blueSwingSpeed = 0f;
    float redSwingSpeed = 0f;
    
    while (!Raylib.WindowShouldClose())
    {
        float deltaTime = Raylib.GetFrameTime();

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            break;
        }
        
        if (gameState == GameState.Start && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            ResetGame();
            gameState = GameState.Playing;
            audio.PlayStart();
        }

        if (gameState == GameState.GameOver && Raylib.IsKeyPressed(KeyboardKey.R))
        {
            ResetGame();
            gameState = GameState.Playing;
        }
        
        Vector2 mouse1 = twoMice.GetDelta(0);
        Vector2 mouse2 = twoMice.GetDelta(1);
        
        saber1Position += mouse1;
        saber2Position += mouse2;
        
        saber1Position.X = Math.Clamp(saber1Position.X, 50, 1230);
        saber1Position.Y = Math.Clamp(saber1Position.Y, 50, 670);
        saber2Position.X = Math.Clamp(saber2Position.X, 50, 1230);
        saber2Position.Y = Math.Clamp(saber2Position.Y, 50, 670);
        
        float saber1X = (saber1Position.X - 640) / 200f;
        float saber1Y = -(saber1Position.Y - 360) / 200f;
        float saber2X = (saber2Position.X - 640) / 200f;
        float saber2Y = -(saber2Position.Y - 360) / 200f;
        
        Vector3 saber1 = new Vector3(saber1X, saber1Y + 1f, 5.5f);
        Vector3 saber2 = new Vector3(saber2X, saber2Y + 1f, 5.5f);

        UpdateSwing(mouse1, deltaTime, ref blueSwingDirection, ref blueSwingSpeed);
        UpdateSwing(mouse2, deltaTime, ref redSwingDirection, ref redSwingSpeed);
        TrackTrail(blueTrail, saber1, deltaTime);
        TrackTrail(redTrail, saber2, deltaTime);
        
        if (gameState == GameState.Playing)
        {
            elapsedTime += deltaTime;
            float objectSpeed = Math.Min(13f, 8f + elapsedTime * 0.25f);
            float spawnInterval = Math.Max(0.62f, 1.05f - elapsedTime * 0.007f);

            spawnTimer -= deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnFormation();
                spawnTimer = spawnInterval;
            }

            foreach (GameObject obj in objects)
            {
                obj.Position.Z += objectSpeed * deltaTime;
                obj.Rotation += deltaTime * 55f;
            }

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                GameObject obj = objects[i];
                Vector3 saber = obj.IsBlue ? saber1 : saber2;
                Vector3 swingDirection = obj.IsBlue ? blueSwingDirection : redSwingDirection;
                float swingSpeed = obj.IsBlue ? blueSwingSpeed : redSwingSpeed;

                if (Vector3.Distance(saber, obj.Position) < 1.5f)
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
                    audio.PlayHit(obj.IsBlue);
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
                    misses++;
                    combo = 0;
                    missFeedbackTimer = 0.45f;
                    shakeTimer = 0.08f;
                    shakeStrength = 0.035f;
                    audio.PlayMiss();
                    if (misses >= 3)
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
        DrawSaber(saber1, Color.Blue);
        DrawSaber(saber2, Color.Red);
        
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
        Raylib.DrawText($"MISSES: {misses}/3", 1030, 50, 25, misses == 0 ? Color.White : Color.Red);
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
        
        if (gameState == GameState.Start)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)170));
            Raylib.DrawText("TWO SABER", 430, 220, 72, Color.White);
            Raylib.DrawText("MOUSE 1 = BLUE", 485, 330, 30, Color.Blue);
            Raylib.DrawText("MOUSE 2 = RED", 490, 370, 30, Color.Red);
            Raylib.DrawText("PRESS SPACE TO START", 420, 470, 30, Color.Yellow);
        }

        if (gameState == GameState.GameOver)
        {
            Raylib.DrawRectangle(0, 0, 1280, 720, new Color((byte)0, (byte)0, (byte)0, (byte)150));
            Raylib.DrawText("GAME OVER", 455, 245, 64, Color.Red);
            Raylib.DrawText($"FINAL SCORE: {score}", 500, 330, 28, Color.White);
            Raylib.DrawText($"FINAL COMBO: {bestCombo}", 500, 370, 28, Color.Yellow);
            Raylib.DrawText($"MISSES: {misses}/3", 500, 410, 28, Color.Red);
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
        spawnTimer = 0f;
        elapsedTime = 0f;
        formationNumber = 0;
        score = 0;
        misses = 0;
        combo = 0;
        bestCombo = 0;
        missFeedbackTimer = 0f;
        shakeTimer = 0f;
        comboMessage = string.Empty;
        comboMessageTimer = 0f;
    }

    void UpdateSwing(Vector2 mouseDelta, float deltaTime, ref Vector3 direction, ref float speed)
    {
        float distance = mouseDelta.Length();
        if (distance > 0.01f && deltaTime > 0f)
        {
            Vector3 movement = new Vector3(mouseDelta.X, -mouseDelta.Y, 0f);
            direction = Vector3.Normalize(movement);
            speed = distance / deltaTime;
        }
        else
        {
            speed = Math.Max(0f, speed - 1800f * deltaTime);
        }
    }

    bool IsComboMilestone(int value)
    {
        return value == 5 || value == 10 || value == 20 || value % 10 == 0;
    }

    void SpawnFormation()
    {
        int formation = formationNumber++;
        float z = -20f;

        if (elapsedTime < 10f)
        {
            AddTarget(RandomLanePosition(), random.Next(2) == 0, z);
            return;
        }

        if (elapsedTime < 24f)
        {
            switch (formation % 3)
            {
                case 0:
                    AddTarget(new Vector3(-1.05f, 1.35f, z), true, z);
                    AddTarget(new Vector3(1.05f, 1.35f, z), false, z);
                    break;
                case 1:
                    AddAlternatingSequence(z, 3);
                    break;
                default:
                    AddTarget(new Vector3(-1.05f, 1.35f, z), true, z);
                    AddTarget(new Vector3(1.05f, 1.35f, z), false, z);
                    break;
            }
            return;
        }

        switch (formation % 4)
        {
            case 0:
                AddAlternatingSequence(z, 5);
                break;
            case 1:
                AddTarget(new Vector3(-1.15f, 1.4f, z), true, z);
                AddTarget(new Vector3(1.15f, 1.4f, z), false, z);
                break;
            case 2:
                AddAlternatingSequence(z, 4);
                break;
            default:
                AddTarget(RandomLanePosition(), random.Next(2) == 0, z);
                break;
        }
    }

    void AddAlternatingSequence(float z, int count)
    {
        float[] lanes = { -2.1f, -0.7f, 0.7f, 2.1f, 0f };

        for (int i = 0; i < count; i++)
        {
            bool isBlue = i % 2 == 0;
            Vector3 position = new Vector3(lanes[i], 1.35f, z - i * 2.4f);
            AddTarget(position, isBlue, position.Z);
        }
    }

    Vector3 RandomLanePosition()
    {
        float[] lanes = { -2.1f, -1.05f, 0f, 1.05f, 2.1f };
        return new Vector3(lanes[random.Next(lanes.Length)], 1.35f, -20f);
    }

    void AddTarget(Vector3 position, bool isBlue, float z)
    {
        objects.Add(new GameObject
        {
            Position = new Vector3(position.X, position.Y, z),
            IsBlue = isBlue,
            Rotation = random.NextSingle() * 360f
        });
    }

    void TrackTrail(List<TrailPoint> trail, Vector3 position, float deltaTime)
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
            trail.Add(new TrailPoint { Position = position, Lifetime = 0.22f });
        }

        while (trail.Count > 12)
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
            byte alpha = (byte)Math.Clamp((int)(current.Lifetime / 0.22f * 150f), 0, 150);
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

    void DrawSaber(Vector3 position, Color color)
    {
        Color outerColor = color == Color.Blue
            ? new Color((byte)30, (byte)120, (byte)255, (byte)90)
            : new Color((byte)255, (byte)30, (byte)30, (byte)90);

        Raylib.DrawCube(position, 0.34f, 2.9f, 0.34f, outerColor);
        Raylib.DrawCube(position, 0.2f, 2.7f, 0.2f, color);
        Raylib.DrawCube(position, 0.08f, 2.55f, 0.08f, Color.White);
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
    }

    public class TrailPoint
    {
        public Vector3 Position;
        public float Lifetime;
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
        Start,
        Playing,
        GameOver
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
        private Music _music;
        private bool _musicLoaded;

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

        public void PlayHit(bool isBlue)
        {
            Play(isBlue ? _blueHit : _redHit);
        }

        public void PlayMiss() => Play(_miss);

        public void PlayCombo() => Play(_combo);

        public void PlayGameOver() => Play(_gameOver);

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

