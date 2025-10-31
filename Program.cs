using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;

// --- 1. Configuración del Servidor Web ---
var builder = WebApplication.CreateBuilder(args);

// Inyectamos nuestro "cerebro" del juego (GameManager)
// 'AddSingleton' asegura que solo haya UNA instancia para toda la app.
// ESTO DEBE IR ANTES de builder.Build()
builder.Services.AddSingleton<GameManager>(); 

// ...
var app = builder.Build(); // Construimos la app

// Habilitar WebSockets
app.UseWebSockets();

// --- CORRECCIÓN AQUÍ ---

// 1. Habilitar que sirva archivos por defecto (index.html, default.htm, etc.)
// DEBE ir ANTES de UseStaticFiles
app.UseDefaultFiles(); 

// 2. Habilitar que sirva archivos estáticos (de la carpeta wwwroot)
app.UseStaticFiles();

// --- FIN DE LA CORRECCIÓN ---

// --- 2. El "Endpoint" de WebSocket ---
// (Asegúrate de que esto vaya DESPUÉS de las líneas de arriba)
app.MapGet("/ws", async (HttpContext context, GameManager gameManager) =>
// ... (el resto de tu código) ...
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        // Aceptamos la conexión
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        
        // Le pasamos el socket a nuestro GameManager
        await gameManager.ManejarConexion(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400; // Bad Request
    }
});

app.Run();

// --- 3. Lógica del Juego (Adaptada de la versión anterior) ---
// (Estas clases van al final del archivo Program.cs o en archivos separados)

public class GameManager
{
    private ConcurrentDictionary<WebSocket, Jugador> _jugadores = new ConcurrentDictionary<WebSocket, Jugador>();
    private ConcurrentDictionary<string, Juego> _juegos = new ConcurrentDictionary<string, Juego>();
    private ConcurrentQueue<Jugador> _jugadorEnEspera = new ConcurrentQueue<Jugador>();

    // Método principal que maneja todo el ciclo de vida de un socket
    public async Task ManejarConexion(WebSocket socket)
    {
        // 1. JUGADOR SE CONECTA
        var nuevoJugador = new Jugador(socket);
        _jugadores.TryAdd(socket, nuevoJugador);

        // 2. INTENTAR EMPAREJAR
        if (_jugadorEnEspera.TryDequeue(out Jugador jugadorQueEsperaba))
        {
            var nuevoJuego = new Juego(jugadorQueEsperaba, nuevoJugador);
            _juegos.TryAdd(nuevoJuego.GameId, nuevoJuego);
            nuevoJuego.IniciarJuego(); // Envía el 'start' a ambos
        }
        else
        {
            _jugadorEnEspera.Enqueue(nuevoJugador);
            await nuevoJugador.EnviarMensajeSimple("waiting", "Esperando a un oponente...");
        }

        // 3. BUCLE DE ESCUCHA DE MENSAJES
        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string mensaje = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    MensajeRecibido(socket, mensaje);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break; // Cliente cerró
                }
            }
            catch
            {
                break; // Error (desconexión abrupta)
            }
        }

        // 4. JUGADOR SE DESCONECTA
        JugadorDesconectado(socket);
    }

    public void MensajeRecibido(WebSocket socket, string mensaje)
    {
        if (!_jugadores.TryGetValue(socket, out Jugador jugador)) return;
        var juego = _juegos.Values.FirstOrDefault(g => g.Jugador1 == jugador || g.Jugador2 == jugador);
        if (juego == null) return;

        dynamic data = JsonConvert.DeserializeObject(mensaje);
        string tipoMensaje = data.type;

        if (tipoMensaje == "shoot")
        {
            int row = data.row;
            int col = data.col;
            juego.ProcesarDisparo(jugador, row, col);
        }
    }

    public void JugadorDesconectado(WebSocket socket)
    {
        if (_jugadores.TryRemove(socket, out Jugador jugadorQueSeFue))
        {
            var juego = _juegos.Values.FirstOrDefault(g => g.Jugador1 == jugadorQueSeFue || g.Jugador2 == jugadorQueSeFue);
            if (juego != null)
            {
                _juegos.TryRemove(juego.GameId, out _);
                Jugador otroJugador = (juego.Jugador1 == jugadorQueSeFue) ? juego.Jugador2 : juego.Jugador1;
                otroJugador.EnviarMensajeSimple("end", "El oponente se ha desconectado.");
            }
        }
    }
}

public class Juego
{
    public string GameId { get; private set; }
    public Jugador Jugador1 { get; private set; }
    public Jugador Jugador2 { get; private set; }
    private Jugador _jugadorEnTurno;
    private int[,] _disparosJ1 = new int[10, 10];
    private int[,] _disparosJ2 = new int[10, 10];

    public Juego(Jugador j1, Jugador j2)
    {
        GameId = Guid.NewGuid().ToString().Substring(0, 8);
        Jugador1 = j1;
        Jugador2 = j2;
        _jugadorEnTurno = j1;
    }

    public async void IniciarJuego()
    {
        Jugador1.PlayerIndex = 0;
        Jugador2.PlayerIndex = 1;
        // JS espera: { type: 'start', ... }
        // El payload se envía como un objeto anónimo
        await Jugador1.Enviar("start", new { gameId = GameId, board = Jugador1.Tablero, you = 0 });
        await Jugador2.Enviar("start", new { gameId = GameId, board = Jugador2.Tablero, you = 1 });
    }

    public async void ProcesarDisparo(Jugador jugadorQueDispara, int row, int col)
    {
        if (jugadorQueDispara != _jugadorEnTurno)
        {
            await jugadorQueDispara.EnviarMensajeSimple("error", "No es tu turno.");
            return;
        }

        Jugador victima = (jugadorQueDispara == Jugador1) ? Jugador2 : Jugador1;
        int[,] tableroDisparos = (jugadorQueDispara == Jugador1) ? _disparosJ1 : _disparosJ2;

        if (tableroDisparos[row, col] != 0)
        {
            await jugadorQueDispara.EnviarMensajeSimple("error", "Ya disparaste a esa casilla.");
            return;
        }

        bool hit = false, sunk = false, gameOver = false;
        int valorEnTableroVictima = victima.Tablero[row, col];

        if (valorEnTableroVictima > 0)
        {
            hit = true;
            tableroDisparos[row, col] = 1;
            victima.Tablero[row, col] *= -1;
            // (Aquí va tu lógica de 'sunk' y 'gameOver')
        }
        else
        {
            hit = false;
            tableroDisparos[row, col] = -1;
        }

        // Enviar resultado al atacante
        await jugadorQueDispara.Enviar("result", new { row, col, hit, sunk, gameOver });
        // Enviar disparo a la víctima
        await victima.Enviar("shot", new { row, col, hit, sunk, gameOver });

        if (!gameOver)
        {
            _jugadorEnTurno = victima;
        }
    }
}

public class Jugador
{
    public WebSocket Socket { get; private set; }
    public int PlayerIndex { get; set; }
    public int[,] Tablero { get; set; }

    // Un único generador 'static' para que no se repitan los tableros
    private static readonly Random _random = new Random();

    public Jugador(WebSocket socket)
    {
        Socket = socket;
        Tablero = new int[10, 10];
        PosicionarBarcos(); // Lógica de ejemplo
    }

    // REEMPLAZA tu método 'PosicionarBarcos' con ESTO:
    private void PosicionarBarcos()
    {
        // Definimos la flota: 1x5, 1x4, 2x3, 1x2
        // Puedes cambiar esto si quieres una flota diferente
        var flota = new List<int> { 5, 4, 3, 3, 2 };
        int shipId = 1; // Usamos un ID de barco incremental (1, 2, 3...)

        foreach (int longitud in flota)
        {
            bool colocado = false;
            while (!colocado)
            {
                // Elige orientación y posición al azar
                int orientacion = _random.Next(2); // 0 = Horizontal, 1 = Vertical
                int fila = _random.Next(10);
                int col = _random.Next(10);

                // Comprueba si el barco cabe ahí
                if (PuedeColocarBarco(fila, col, orientacion, longitud))
                {
                    // Si cabe, lo "pinta" en el tablero
                    for (int i = 0; i < longitud; i++)
                    {
                        if (orientacion == 0) // Horizontal
                        {
                            Tablero[fila, col + i] = shipId;
                        }
                        else // Vertical
                        {
                            Tablero[fila + i, col] = shipId;
                        }
                    }
                    colocado = true; // Pasa al siguiente barco
                    shipId++;
                }
                // Si no cabe, el bucle 'while' repite hasta encontrar un lugar
            }
        }
    }

    // AÑADE ESTE NUEVO MÉTODO (es el ayudante del anterior)
    private bool PuedeColocarBarco(int fila, int col, int orientacion, int longitud)
    {
        for (int i = 0; i < longitud; i++)
        {
            int r = fila;
            int c = col;

            if (orientacion == 0) // Horizontal
            {
                c += i;
            }
            else // Vertical
            {
                r += i;
            }

            // 1. Comprobar si se sale del tablero
            if (r >= 10 || c >= 10)
            {
                return false;
            }

            // 2. Comprobar si choca con otro barco (Regla simple)
            // (Una regla estricta también revisaría celdas adyacentes)
            if (Tablero[r, c] != 0)
            {
                return false;
            }
        }
        
        // Si pasó todas las comprobaciones, es un lugar válido
        return true;
    }

    // ... (Aquí sigue el resto de tu clase Jugador: Enviar, EnviarMensajeSimple, etc.) ...

    // Helper para enviar JSON. Tu JS espera { type: '...' }
    public async Task Enviar(string tipo, object payload)
    {
        if (Socket.State != WebSocketState.Open) return;
        
        // ASP.NET Core tiene su propio serializador, pero Newtonsoft es fácil
        // Tu JS *no* espera un 'payload', sino los campos directamente
        
        string jsonPayload = JsonConvert.SerializeObject(payload);
        string jsonWrapper = $"{{ \"type\": \"{tipo}\", {jsonPayload.Substring(1)}"; // Combina los JSON
        
        var buffer = Encoding.UTF8.GetBytes(jsonWrapper);
        await Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task EnviarMensajeSimple(string tipo, string msg)
    {
        if (Socket.State != WebSocketState.Open) return;
        var wrapper = new { type = tipo, msg = msg };
        string json = JsonConvert.SerializeObject(wrapper);
        var buffer = Encoding.UTF8.GetBytes(json);
        await Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}