"use strict";

const fs = require("node:fs");
const path = require("node:path");

const projectRoot = path.resolve(__dirname, "..");
const viewer = JSON.parse(fs.readFileSync(
    path.join(projectRoot, ".runtime", "workbench-viewer.json"),
    "utf8",
));

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function waitForTarget() {
    for (let attempt = 0; attempt < 30; attempt += 1) {
        try {
            const response = await fetch("http://127.0.0.1:9222/json/list");
            const targets = await response.json();
            const page = targets.find((target) => target.type === "page");
            if (page?.webSocketDebuggerUrl) {
                return page;
            }
        } catch {
            // Workbench is still starting.
        }
        await sleep(500);
    }
    throw new Error("Timed out waiting for Workbench.");
}

async function main() {
    const target = await waitForTarget();
    const socket = new WebSocket(target.webSocketDebuggerUrl);
    let nextId = 1;
    const pending = new Map();

    await new Promise((resolve, reject) => {
        socket.addEventListener("open", resolve, { once: true });
        socket.addEventListener("error", reject, { once: true });
    });

    socket.addEventListener("message", (event) => {
        const message = JSON.parse(event.data);
        if (message.id && pending.has(message.id)) {
            pending.get(message.id)(message);
            pending.delete(message.id);
        }
    });

    const send = (method, params = {}) => new Promise((resolve, reject) => {
        const id = nextId++;
        const timer = setTimeout(() => {
            pending.delete(id);
            reject(new Error(`CDP operation timed out: ${method}`));
        }, 10000);
        pending.set(id, (message) => {
            clearTimeout(timer);
            if (message.error) {
                reject(new Error(message.error.message));
                return;
            }
            resolve(message.result);
        });
        socket.send(JSON.stringify({ id, method, params }));
    });

    await send("Runtime.evaluate", {
        expression: `(() => {
            const input = Array.from(document.querySelectorAll("input[type='password']"))
                .find((element) => element.offsetParent !== null);
            if (!input) throw new Error("Password input is not visible.");
            input.focus();
            return "focused";
        })()`,
        returnByValue: true,
    });

    await send("Input.insertText", { text: viewer.password });

    const submitted = await send("Runtime.evaluate", {
        expression: `(() => {
            const input = Array.from(document.querySelectorAll("input[type='password']"))
                .find((element) => element.offsetParent !== null);
            const dialog = input?.closest(".dialog");
            const ok = dialog && Array.from(dialog.querySelectorAll("#ok"))
                .find((element) => element.offsetParent !== null);
            if (!input || !ok) throw new Error("Password dialog controls are missing.");
            const length = input.value.length;
            ok.click();
            return { submitted: true, length };
        })()`,
        returnByValue: true,
    });

    socket.close();
    console.log(JSON.stringify(submitted.result.value));
}

main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
