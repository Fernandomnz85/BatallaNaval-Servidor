# 🚢 Batalla Naval - Proyecto (ASP.NET Core + WebSockets)

Este es un proyecto universitario que implementa el clásico juego de Batalla Naval (Hundir la Flota) en un entorno web multijugador. Utiliza un back-end de ASP.NET Core (C#) y un front-end de JavaScript puro, con comunicación en tiempo real a través de WebSockets nativos.

## ✨ Características

* **Multijugador en Tiempo Real:** Dos jugadores pueden conectarse y jugar una partida completa desde sus navegadores.
* **Servidor Autoritativo:** Toda la lógica del juego (turnos, disparos, validaciones, victorias) se maneja en el servidor C# para prevenir trampas.
* **Generación Aleatoria de Tableros:** Los barcos se posicionan aleatoriamente en el servidor al inicio de cada partida. ¡No hay dos partidas iguales!
* **Cliente Ligero:** El front-end está escrito en JavaScript puro (Vanilla JS), sin *frameworks*, y su única tarea es enviar acciones y renderizar el estado que recibe del servidor.

---

## 💻 Pila Tecnológica

* **Back-end:** C# con ASP.NET Core
* **Comunicación:** WebSockets Nativos (API `WebSocket` del navegador y `System.Net.WebSockets` en .NET).
* **Front-end:** JavaScript (ES6+), HTML5, CSS (sin librerías).
* **Serialización:** Newtonsoft.Json (para los mensajes entre cliente y servidor).

---

## 🛠️ ¿Cómo Funciona? (Arquitectura)

Este proyecto utiliza una arquitectura de cliente-servidor simple y moderna:

1.  **Servidor (`Program.cs`):** Inicia una aplicación de ASP.NET Core que cumple dos funciones:
    * **Servir Archivos Estáticos:** Sirve los archivos `index.html` y `main.js` desde la carpeta `wwwroot`.
    * **Endpoint de WebSocket:** Abre un endpoint de comunicación en la ruta `/ws`.
2.  **Cliente (`main.js`):** Cuando el `index.html` se carga, el JavaScript del cliente intenta conectarse inmediatamente al endpoint `/ws`.
3.  **GameManager:** Una clase `GameManager` (singleton) en el servidor C# recibe todas las nuevas conexiones de WebSocket.
    * **Emparejamiento:** El `GameManager` toma al primer jugador y lo pone en una cola de espera. Cuando llega el segundo, crea una instancia de `Juego` para ambos.
    * **Lógica del Juego:** El `GameManager` maneja toda la partida (turnos, lógica de disparos, etc.) basándose en los mensajes JSON que recibe de los clientes (ej. `{type: 'shoot'}`).
    * **Actualizaciones de Estado:** El servidor notifica a los clientes sobre el estado del juego enviando mensajes JSON (ej. `{type: 'start'}`, `{type: 'result'}`, `{type: 'shot'}`).

---

## 🚀 Cómo Correr el Proyecto (Tutorial)

Para ejecutar este proyecto en tu máquina local, solo necesitas el SDK de .NET.

### 1. Prerrequisitos

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o la versión usada en el proyecto).
* [Git](https://git-scm.com/downloads).

### 2. Pasos de Instalación y Ejecución

1.  **Clona el Repositorio:**
    ```bash
    git clone [https://github.com/](https://github.com/)[TU_USUARIO]/[TU_REPOSITORIO].git
    ```

2.  **Navega a la Carpeta del Proyecto:**
    ```bash
    cd BatallaNaval.ServidorWeb
    ```

3.  **Restaura los Paquetes:**
    Este comando descargará `Newtonsoft.Json` y otras dependencias.
    ```bash
    dotnet restore
    ```

4.  **Ejecuta el Servidor:**
    ```bash
    dotnet run
    ```

5.  **¡Juega!**
    * La terminal te mostrará un mensaje como `Now listening on: http://localhost:5062`.
    * Abre esa URL (ej. `http://localhost:5062`) en tu navegador.
    * Para jugar, abre esa **misma URL** en una **segunda pestaña** o en un navegador en modo incógnito.
    * ¡El juego comenzará automáticamente!

---

## 🌐 Cómo Jugar Online (con Dev Tunnels)

¿Quieres jugar con un amigo sin estar en la misma red? Puedes usar **Dev Tunnels** de VS Code para crear un enlace público temporal.

1.  **Abre el proyecto** en **VS Code**.
2.  **Instala las Extensiones:**
    * `C# Dev Kit` (de Microsoft).
    * `Remote - Tunnels` (ID: `ms-vscode.remote-server`, de Microsoft).
3.  **Ejecuta el proyecto** en la terminal de VS Code:
    ```bash
    dotnet run
    ```
4.  **Anota el puerto** (ej. `5062`).
5.  **Activa el Túnel:**
    * Abre la Paleta de Comandos (`Ctrl+Shift+P`).
    * Escribe y selecciona `Remote - Tunnels: Turn on Remote Tunnel Access...`.
    * Inicia sesión con tu cuenta de Microsoft o GitHub.
6.  **Haz el Puerto Público:**
    * Ve a la pestaña **"Puertos" (Ports)** en el panel inferior de VS Code.
    * Busca tu puerto (ej. `5062`), haz clic derecho sobre él.
    * Cambia la "Visibilidad del Puerto" a **Público (Public)**.
7.  **Comparte el Enlace:**
    * En esa misma pestaña "Puertos", copia la **"Dirección Reenviada"** (será algo como `https://[nombre-raro].devtunnels.ms:5062`).
    * ¡Cualquier persona con ese enlace podrá conectarse a tu servidor y jugar!
