"use strict";

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
                expression: `JSON.stringify({
                    bodyText: (document.body?.innerText ?? "").slice(0, 4000),
                    html: (document.body?.innerHTML ?? "").slice(0, 12000),
                    buttons: Array.from(document.querySelectorAll("button")).map((element, index) => ({
                        index,
                        text: (element.innerText || "").trim(),
                        ariaLabel: element.getAttribute("aria-label"),
                        title: element.getAttribute("title")
                    })).filter((item) => item.text || item.ariaLabel || item.title),
                    inputs: Array.from(document.querySelectorAll("input")).map((element, index) => ({
                        index,
                        type: element.type,
                        name: element.name,
                        id: element.id,
                        placeholder: element.placeholder,
                        value: element.value
                    })),
                    clickable: Array.from(document.querySelectorAll("[role='button'],a,[role='menuitem'],[tabindex]")).map((element, index) => ({
                        index,
                        tag: element.tagName,
                        text: (element.innerText || "").trim().slice(0, 120),
                        className: String(element.className || "").slice(0, 200),
                        ariaLabel: element.getAttribute("aria-label"),
                        title: element.getAttribute("title"),
                        html: element.outerHTML.slice(0, 500)
                    }))
                })`,
                returnByValue: true,
            },
        }));
    });
    socket.addEventListener("message", (event) => {
        const message = JSON.parse(event.data);
        if (message.id !== 1) {
            return;
        }
        clearTimeout(timer);
        resolve(JSON.parse(message.result.result.value));
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
