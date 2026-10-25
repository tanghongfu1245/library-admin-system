"use strict";

const fs = require("node:fs");
const path = require("node:path");

const projectRoot = path.resolve(__dirname, "..");
const viewer = JSON.parse(fs.readFileSync(
    path.join(projectRoot, ".runtime", "workbench-viewer.json"),
    "utf8",
));

const value = JSON.stringify(viewer);
const expression = `(() => {
    const viewer = ${value};
    const setValue = (id, value) => {
        const element = document.getElementById(id);
        if (!element) throw new Error("Missing field: " + id);
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set;
        setter.call(element, String(value));
        element.dispatchEvent(new Event("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
    };
    setValue("caption", "图书管理员系统");
    setValue("description", "本地图书管理系统只读连接");
    setValue("userName", viewer.user);
    setValue("hostName", viewer.host);
    setValue("port", viewer.port);
    setValue("defaultSchema", viewer.database);
    document.getElementById("advanced-btn")?.click();
    return "connection form submitted for testing";
})()`;

const { execFileSync } = require("node:child_process");
const output = execFileSync(
    process.execPath,
    [path.join(__dirname, "workbench-cdp.cjs"), expression],
    { encoding: "utf8" },
);
console.log(output.trim());
