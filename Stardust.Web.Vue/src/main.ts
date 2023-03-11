import { createCubeUI, fileContext } from "CubeUI";
import "element-plus/dist/index.css";
import { createApp } from "vue";
import "../lib/style.css";
import App from "./App.vue";

const cubeUI = createCubeUI();

const app = createApp(App);
app.use(cubeUI);

const store = app.config.globalProperties.$store;

// 注册组件
const files = import.meta.glob("@/**/*.(vue|tsx)", { eager: true });

store.dispatch("setFiles", files);

fileContext.addFiles(files);

// store.dispatch('setUrls', { baseUrl: 'http://localhost:5000' })
store.dispatch("setUrls", { baseUrl: "http://star.newlifex.com" });

app.mount("#app");
