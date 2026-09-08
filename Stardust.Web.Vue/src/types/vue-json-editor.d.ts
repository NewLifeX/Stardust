// 兜底声明：Cube 内部封装的 vue-json-editor 末随 package.json "exports" 暴露类型，
// tsconfig paths 将 `vue-json-editor` 接管到本文件，避免 TS7016（bundler 解析下
// 会去解析到真实 .vue 文件却找不到声明）。本工程不直接使用该包，仅声明为 any 即可。
declare const JsonEditor: any;
export default JsonEditor;