// The browser studio's own reach into the page, beside Kaldra's and Sirrow's: the project's files kept in IndexedDB
// (BrowserFiles), and a file picked to import. The studio keeps its files in memory and reads them from there; this
// loads them once as it starts and writes each change behind it.

const DATABASE = 'lunelle-studio';
const STORE = 'files';

let db = null;
let loaded = new Map();
let picked = null;

function done(request) {
	return new Promise((resolve, reject) => {
		request.onsuccess = () => resolve(request.result);
		request.onerror = () => reject(request.error);
	});
}

// Opens the store and loads every file in it; false when the browser won't keep anything (a private window, storage
// blocked), and the studio then works on files that last until the page closes
export async function filesOpen() {
	try {
		// Asked once: kept files are then not cleared when the browser runs short of space
		navigator.storage?.persist?.().catch(() => { });
		const open = indexedDB.open(DATABASE, 1);
		open.onupgradeneeded = () => open.result.createObjectStore(STORE);
		db = await done(open);
		const store = db.transaction(STORE, 'readonly').objectStore(STORE);
		const [keys, values] = await Promise.all([done(store.getAllKeys()), done(store.getAll())]);
		keys.forEach((key, i) => loaded.set(key, values[i]));
		return true;
	} catch (e) {
		console.warn(`[studio] the browser's storage can't be opened, so nothing is kept: ${e}`);
		db = null;
		return false;
	}
}

// The paths loaded, joined by newlines
export function filesLoaded() {
	return [...loaded.keys()].join('\n');
}

// A loaded file's bytes, given up once taken
export function filesTake(path) {
	const bytes = loaded.get(path);
	loaded.delete(path);
	return bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes ?? 0);
}

function write(change) {
	if (!db) return;
	try {
		const transaction = db.transaction(STORE, 'readwrite');
		change(transaction.objectStore(STORE));
		transaction.onerror = () => console.warn(`[studio] a file couldn't be kept: ${transaction.error}`);
	} catch (e) {
		console.warn(`[studio] a file couldn't be kept: ${e}`);
	}
}

export function filesPut(path, bytes) {
	const copy = bytes.slice();
	write(store => store.put(copy, path));
}

export function filesDelete(path) {
	write(store => store.delete(path));
}

// Asks for a file, then whether to replace the music with it: its name once it is read (taken with pickedTake), or
// nothing when the player changes their mind
export function pickFile(accept, question) {
	return new Promise(resolve => {
		const input = document.createElement('input');
		input.type = 'file';
		input.accept = accept;
		input.onchange = async () => {
			const file = input.files?.[0];
			if (!file || !confirm(question.replace('{0}', file.name))) {
				resolve('');
				return;
			}
			picked = new Uint8Array(await file.arrayBuffer());
			resolve(file.name);
		};
		input.oncancel = () => resolve('');
		input.click();
	});
}

export function pickedTake() {
	const bytes = picked ?? new Uint8Array(0);
	picked = null;
	return bytes;
}
