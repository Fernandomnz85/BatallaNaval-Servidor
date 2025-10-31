/*
 * Client-side logic for the Battleship game. This script connects to the
 * WebSocket server, builds two interactive 10×10 grids for the player's
 * board and the enemy board, handles user input (firing shots) and
 * updates the UI in response to messages from the server.
 */

const statusEl = document.getElementById('status');
const logEl = document.getElementById('log');
const yourBoardEl = document.getElementById('yourBoard');
const enemyBoardEl = document.getElementById('enemyBoard');

// Determine WebSocket protocol based on current page protocol
const wsProtocol = location.protocol === 'https:' ? 'wss' : 'ws';
const ws = new WebSocket(`${wsProtocol}://${location.host}/ws`);

// Internal state
let gameId = null;
let myTurn = false;
let myBoard = null; // 2D array of numbers from server
let enemyBoard = null; // 2D array with unknown/hit/miss/sunk markers
let myBoardEls = []; // DOM elements for player's board
let enemyBoardEls = []; // DOM elements for enemy board
let gameOver = false;

// Utility to log messages to the textarea
function log(msg) {
  logEl.textContent += msg + '\n';
  logEl.scrollTop = logEl.scrollHeight;
}

// Convert row/col to human-readable cell name (e.g. A5)
function toCellName(row, col) {
  const letters = 'ABCDEFGHIJ';
  return letters[row] + (col + 1);
}

// Build a 10×10 grid of cell elements and attach click handlers for the enemy board
function buildBoards() {
  yourBoardEl.innerHTML = '';
  enemyBoardEl.innerHTML = '';
  myBoardEls = [];
  enemyBoardEls = [];
  for (let r = 0; r < 10; r++) {
    myBoardEls[r] = [];
    enemyBoardEls[r] = [];
    for (let c = 0; c < 10; c++) {
      // Player board cell
      const myCell = document.createElement('div');
      myCell.classList.add('cell', 'water');
      myCell.dataset.row = r;
      myCell.dataset.col = c;
      yourBoardEl.appendChild(myCell);
      myBoardEls[r][c] = myCell;
      // Enemy board cell
      const enemyCell = document.createElement('div');
      enemyCell.classList.add('cell', 'water');
      enemyCell.dataset.row = r;
      enemyCell.dataset.col = c;
      enemyCell.addEventListener('click', () => {
        if (gameOver) return;
        if (!myTurn) {
          log('⏳ No es tu turno.');
          return;
        }
        const row = parseInt(enemyCell.dataset.row);
        const col = parseInt(enemyCell.dataset.col);
        // Ignore if already fired at this cell
        if (enemyBoard[row][col] !== 0) {
          log('⚠️ Ya disparaste a esa casilla.');
          return;
        }
        // Send shot to server
        ws.send(
          JSON.stringify({ type: 'shoot', gameId, row: row, col: col })
        );
        // Prevent multiple rapid shots
        myTurn = false;
        statusEl.textContent = 'Esperando resultado...';
      });
      enemyBoardEl.appendChild(enemyCell);
      enemyBoardEls[r][c] = enemyCell;
    }
  }
}

// Update UI for player's board based on myBoard state
function updateMyBoard() {
  for (let r = 0; r < 10; r++) {
    for (let c = 0; c < 10; c++) {
      const cellVal = myBoard[r][c];
      const cellEl = myBoardEls[r][c];
      cellEl.classList.remove('ship', 'water', 'hit', 'miss', 'sunk');
      if (cellVal > 0) {
        cellEl.classList.add('ship');
      } else if (cellVal === 0) {
        cellEl.classList.add('water');
      } else {
        // negative values: hit or miss
        if (cellVal === -10) {
          cellEl.classList.add('miss');
        } else {
          cellEl.classList.add('hit');
        }
      }
    }
  }
}

// Update UI for enemy board based on enemyBoard state
function updateEnemyBoard() {
  for (let r = 0; r < 10; r++) {
    for (let c = 0; c < 10; c++) {
      const state = enemyBoard[r][c];
      const cellEl = enemyBoardEls[r][c];
      cellEl.classList.remove('water', 'hit', 'miss', 'sunk');
      if (state === 0) {
        cellEl.classList.add('water');
      } else if (state === -1) {
        cellEl.classList.add('miss');
      } else if (state === 1) {
        cellEl.classList.add('hit');
      } else if (state === 2) {
        cellEl.classList.add('sunk');
      }
    }
  }
}

ws.onopen = () => {
  statusEl.textContent = 'Conectado al servidor...';
};

ws.onmessage = (event) => {
  let data;
  try {
    data = JSON.parse(event.data);
  } catch {
    return;
  }
  switch (data.type) {
    case 'waiting':
      statusEl.textContent = data.msg;
      log('🕒 ' + data.msg);
      break;
    case 'start':
      gameId = data.gameId;
      myBoard = data.board;
      // Initialize enemy board with all unknown (0)
      enemyBoard = Array.from({ length: 10 }, () => Array(10).fill(0));
      buildBoards();
      updateMyBoard();
      updateEnemyBoard();
      myTurn = data.you === 0;
      statusEl.textContent = myTurn
        ? 'Juego iniciado. Tu turno.'
        : 'Juego iniciado. Turno del rival.';
      log(
        '🎮 Juego iniciado (ID ' +
          gameId +
          '). Eres el jugador #' +
          (data.you + 1) +
          '.\n'
      );
      break;
    case 'result':
      // Our shot result
      if (gameOver) return;
      {
        const { row, col, hit, sunk, gameOver: ended } = data;
        if (hit) {
          enemyBoard[row][col] = sunk ? 2 : 1;
          log(
            '💥 Disparaste a ' +
              toCellName(row, col) +
              ' → ' +
              (sunk ? 'Hundido' : 'Impacto')
          );
        } else {
          enemyBoard[row][col] = -1;
          log('💧 Disparaste a ' + toCellName(row, col) + ' → Agua');
        }
        updateEnemyBoard();
        if (ended) {
          gameOver = true;
          statusEl.textContent = '🏆 ¡Ganaste!';
          log('🎉 ¡Has hundido todos los barcos enemigos!');
        } else {
          // Now it's opponent's turn; wait for their shot
          myTurn = false;
          statusEl.textContent = 'Turno del rival';
        }
      }
      break;
    case 'shot':
      // Opponent fired at us
      if (gameOver) return;
      {
        const { row, col, hit, sunk, gameOver: ended } = data;
        // Update our board representation (mark negative values)
        if (myBoard[row][col] > 0) {
          myBoard[row][col] = -myBoard[row][col];
        } else if (myBoard[row][col] === 0) {
          myBoard[row][col] = -10;
        }
        updateMyBoard();
        log(
          (hit ? '🔥 ' : '💦 ') +
            'El rival disparó a ' +
            toCellName(row, col) +
            ' → ' +
            (hit ? (sunk ? 'Hundido' : 'Impacto') : 'Agua')
        );
        if (ended) {
          gameOver = true;
          statusEl.textContent = '😞 Has perdido';
          log('💀 Todos tus barcos han sido hundidos.');
        } else {
          // Now it's our turn
          myTurn = true;
          statusEl.textContent = 'Tu turno';
        }
      }
      break;
    case 'end':
      gameOver = true;
      statusEl.textContent = 'Partida terminada';
      log('⚠️ ' + data.msg);
      break;
    case 'error':
      log('⚠️ ' + data.msg);
      break;
    default:
      break;
  }
};

ws.onclose = () => {
  if (!gameOver) {
    statusEl.textContent = 'Conexión cerrada';
    log('⚠️ Conexión cerrada por el servidor.');
  }
};