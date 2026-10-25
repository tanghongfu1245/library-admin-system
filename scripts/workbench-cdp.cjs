"use strict";

const expression = process.argv[2];
if (!expression) {
    throw new Error("Usage: node workbench-cdp.cjs <javascript-expression>");
}

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
    const result = await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error("CDP evaluation timed out.")), 10000);
        socket.addEventListener("open", () => {
            socket.send(JSON.stringify({
                id: 1,
                method: "Runtime.evaluate",
                params: {
                    expression,
                    returnByValue: true,
                    awaitPromise: true,
                },
            }));
        });
        socket.addEventListener("message", (event) => {
            const message = JSON.parse(event.data);
            if (message.id !== 1) {
                return;
            }
            clearTimeout(timer);
            resolve(message.result);
            socket.close();
        });
        socket.addEventListener("error", (error) => {
            clearTimeout(timer);
            reject(error);
        });
    });

    console.log(JSON.stringify(result, null, 2));
}

main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
