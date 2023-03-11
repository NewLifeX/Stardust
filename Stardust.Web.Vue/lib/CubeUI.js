var Nt = Object.defineProperty;
var jt = (e, t, l) => t in e ? Nt(e, t, { enumerable: !0, configurable: !0, writable: !0, value: l }) : e[t] = l;
var ve = (e, t, l) => (jt(e, typeof t != "symbol" ? t + "" : t, l), l);
import { defineComponent as k, resolveComponent as m, openBlock as s, createBlock as v, mergeProps as O, createElementBlock as h, normalizeStyle as M, withCtx as u, createTextVNode as I, toDisplayString as w, createCommentVNode as T, createVNode as i, Fragment as S, renderList as U, createElementVNode as d, renderSlot as W, pushScopeId as B, popScopeId as z, withDirectives as A, vShow as N, normalizeClass as P, resolveDirective as Re, createSlots as Lt, h as Mt } from "vue";
import * as Rt from "element-plus";
import { ElConfigProvider as Bt, ElMessage as x, ElMessageBox as we } from "element-plus";
import zt from "axios";
import * as qt from "@element-plus/icons-vue";
import { createWebHistory as Ht, createRouter as xt } from "vue-router";
import { createStore as Kt } from "vuex";
const Wt = k({
  name: "FormControl",
  props: {
    // modelValue: [String, Number, Object, Boolean, Array],
    modelValue: [Object],
    // 字段配置
    configs: {
      type: Object,
      default: {}
    }
  },
  emits: ["update:modelValue"],
  data() {
    return {
      model: void 0,
      shortcuts: [
        {
          text: "昨天",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 1), e.setTime(e.getTime() - 3600 * 1e3 * 24 * 1), [t, e];
          }
        },
        {
          text: "今天",
          value() {
            const e = new Date();
            return [new Date(), e];
          }
        },
        {
          text: "最近一周",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 7), [t, e];
          }
        },
        {
          text: "最近一个月",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 30), [t, e];
          }
        }
      ],
      // 选项列表
      dataList: []
    };
  },
  computed: {
    // ui组件的配置
    options() {
      let e = this, t = {}, l = e.configs;
      if (!l.options)
        return t;
      let a = new URLSearchParams(l.options);
      for (const n of a) {
        let o = n[1];
        t[n[0]] = o;
      }
      return t;
    },
    name() {
      return this.configs.name;
    },
    selectOptions() {
      return this.dataList.map(
        (e) => new Object({
          label: e[this.options.labelField || "label"],
          // 如果是多选，由于数组转逗号隔开的字符串，再转成数组，值就变成字符串，因此统一处理为字符串
          value: this.options.multiple === "true" ? this.getValueByDataType(e, this.options) + "" : this.getValueByDataType(e, this.options)
        })
      );
    }
  },
  watch: {
    model(e, t) {
      let l = this.name, a = this.configs, n = this.options, o = this.modelValue;
      if (l.includes("$")) {
        let r = e, c = l.split("$");
        a.itemType === "datePicker" && a.options && a.options.includes("type=daterange") ? this.valueValidate(r) ? (o[c[0]] = r[0], o[c[1]] = r[1]) : (o[c[0]] = void 0, o[c[1]] = void 0) : a.itemType === "checkbox" ? this.valueValidate(r) ? o[c[0]] = r.join() : o[c[0]] = void 0 : a.itemType === "select" && n.multiple === "true" ? this.valueValidate(r) ? o[c[0]] = r.join() : o[c[0]] = void 0 : console.warn("表单名中带$，但是未配置处理器");
      } else
        a.itemType === "select" && !this.valueValidate(e) && (e = void 0), o[l] = e;
    },
    // 外部修改此值，以便传递到model
    modelValue: {
      handler(e, t) {
        let l = this.name, a = e[this.name], n = this.configs, o = this.options;
        if (l.includes("$")) {
          let r = l.split("$");
          if (n.itemType === "datePicker" && n.options && n.options.includes("type=daterange"))
            this.valueValidate(a) ? (this.model = [a[0], a[1]], this.modelValue[l] = void 0) : (!this.modelValue[r[0]] || !this.modelValue[r[1]]) && (this.model = void 0);
          else if (n.itemType === "checkbox")
            if (a = e[r[0]], this.valueValidate(a)) {
              let c = a.split(",");
              this.model = c;
            } else
              this.model = void 0;
          else if (n.itemType === "select" && o.multiple === "true")
            if (a = e[r[0]], this.valueValidate(a)) {
              let c = a.split(",");
              this.model = c;
            } else
              this.model = [];
        } else
          n.itemType === "select" && o.multiple === "true" ? this.model = a || [] : this.model = a;
      },
      // 加了此选项，就不用再created赋值
      immediate: !0,
      deep: !0
    },
    "configs.url": {
      handler() {
        this.getData();
      }
    },
    // 获取远程数据的参数
    "configs.data": {
      handler() {
        this.getData();
      },
      deep: !0
    }
    // !!! 不能监听，外部如果共用configs，那么所有用了该configs的组件都会受影响
    // 'configs.dataList': {
    //   handler() {
    //     this.dataList = this.configs.dataList
    //   },
    //   deep: true
    // }
  },
  created() {
    this.getData();
  },
  methods: {
    // 设置请求参数，configs.data。对options.data进行处理，如果值有{{field}}形式的值，则替换成为model中的值
    setData() {
      if (!this.options.data)
        return;
      let e = JSON.parse(this.options.data);
      this.configs.data || (this.configs.data = {});
      for (const t in e)
        if (Object.prototype.hasOwnProperty.call(e, t)) {
          const l = e[t];
          typeof l == "string" && l.startsWith("{{") && l.endsWith("}}") ? this.modelValue && (this.configs.data[t] = this.modelValue[l.substring(2, l.length - 2)]) : this.configs.data[t] = l;
        }
    },
    // 类似多选下拉的组件，设置默认值
    setDefaultValue() {
      let e = this, t = e.options, l = e.modelValue;
      if (e.dataList) {
        if (!t.getValueField || !t.getLabelField)
          return;
        let a = l[t.getValueField];
        if (!a)
          return;
        let n = {
          [t.labelField]: l[t.getLabelField] || a,
          [t.valueField]: a
        };
        e.dataList.push(n);
      }
    },
    getData() {
      let e = this, t = e.configs.url;
      if (t) {
        if (e.options.remote === "true") {
          this.setDefaultValue();
          return;
        }
        if (typeof t == "object" && t.length > 0) {
          e.dataList = t;
          return;
        }
        if (typeof t != "string") {
          console.warn("配置中url不正确，不进行处理", t);
          return;
        }
        t.startsWith("[") ? e.getLocalData() : t.startsWith("/") || t.startsWith("http") ? e.getRemoteData() : console.warn("配置中url不正确，不进行处理", t);
      }
    },
    // 获取远程数据
    getRemoteData(e = "") {
      let t = this, l = t.configs.url, a = t.options.method || "post", n = a === "post" ? {
        txtKeywords: e
      } : void 0;
      this.setData(), t.configs.data && (n = { ...n, ...t.configs.data });
      let o = {
        url: l,
        method: a,
        data: n
      };
      t.$http(o).then((r) => {
        const c = r.data;
        t.dataList = c.rows || c.list || c;
      });
    },
    // 解析url中的数据
    getLocalData() {
      const e = this, t = JSON.parse(e.configs.url);
      e.dataList = t;
    },
    getValueByDataType(e, t) {
      let l = e[t.valueField || "value"];
      return (t.dataType === "Int32" || t.dataType === "int") && (l = parseInt(l)), l;
    },
    /**
     * 有效值验证，不为undefined、空字符串、null
     */
    valueValidate(e) {
      return e !== "" && e !== void 0 && e !== null;
    }
  }
}), D = (e, t) => {
  const l = e.__vccOpts || e;
  for (const [a, n] of t)
    l[a] = n;
  return l;
};
function Gt(e, t, l, a, n, o) {
  const r = m("el-select-v2"), c = m("el-radio"), _ = m("el-checkbox"), b = m("el-checkbox-group"), $ = m("el-date-picker"), y = m("el-input");
  return e.configs.itemType === "select" ? (s(), v(r, O({
    key: 0,
    modelValue: e.model,
    "onUpdate:modelValue": t[0] || (t[0] = (p) => e.model = p),
    size: "default",
    style: { width: e.configs.width ? e.configs.width : "220px" },
    placeholder: e.options.placeholder || "请选择" + e.configs.displayName,
    remote: e.options.remote === "true",
    "remote-method": e.getRemoteData,
    "allow-create": e.options.allowCreate === "true",
    multiple: e.options.multiple === "true",
    options: e.selectOptions,
    filterable: "",
    clearable: ""
  }, e.$attrs), null, 16, ["modelValue", "style", "placeholder", "remote", "remote-method", "allow-create", "multiple", "options"])) : e.configs.itemType === "radio" ? (s(), h("div", {
    key: 1,
    style: M({ width: e.configs.width ? e.configs.width : "220px" })
  }, [
    e.valueValidate(e.options.value1) ? (s(), v(c, O({
      key: 0,
      modelValue: e.model,
      "onUpdate:modelValue": t[1] || (t[1] = (p) => e.model = p),
      label: typeof e.model == "number" ? parseInt(e.options.value1) : e.options.value1
    }, e.$attrs), {
      default: u(() => [
        I(w(e.options.label1), 1)
      ]),
      _: 1
    }, 16, ["modelValue", "label"])) : T("", !0),
    e.valueValidate(e.options.value2) ? (s(), v(c, O({
      key: 1,
      modelValue: e.model,
      "onUpdate:modelValue": t[2] || (t[2] = (p) => e.model = p),
      label: typeof e.model == "number" ? parseInt(e.options.value2) : e.options.value2
    }, e.$attrs), {
      default: u(() => [
        I(w(e.options.label2), 1)
      ]),
      _: 1
    }, 16, ["modelValue", "label"])) : T("", !0)
  ], 4)) : e.configs.itemType === "checkbox" ? (s(), h("div", {
    key: 2,
    style: M({ width: e.configs.width ? e.configs.width : "220px" })
  }, [
    i(b, O({
      modelValue: e.model,
      "onUpdate:modelValue": t[3] || (t[3] = (p) => e.model = p)
    }, e.$attrs), {
      default: u(() => [
        (s(!0), h(S, null, U(e.dataList, (p) => (s(), v(_, {
          label: e.getValueByDataType(p, e.options)
        }, {
          default: u(() => [
            I(w(p[e.options.labelField || "label"]), 1)
          ]),
          _: 2
        }, 1032, ["label"]))), 256))
      ]),
      _: 1
    }, 16, ["modelValue"])
  ], 4)) : e.configs.itemType === "datePicker" ? (s(), v($, O({
    key: 3,
    modelValue: e.model,
    "onUpdate:modelValue": t[4] || (t[4] = (p) => e.model = p),
    style: { width: e.configs.width ? e.configs.width : "220px" },
    size: "default",
    class: "date-time-picker",
    type: e.options.type,
    format: "YYYY-MM-DD",
    "value-format": "YYYY-MM-DD",
    "range-separator": "至",
    "start-placeholder": "开始时间",
    "end-placeholder": "结束时间",
    shortcuts: e.shortcuts
  }, e.$attrs), null, 16, ["modelValue", "style", "type", "shortcuts"])) : (s(), v(y, O({
    key: 4,
    size: "default",
    style: { width: e.configs.width ? e.configs.width : "220px" },
    "prefix-icon": e.options.icon,
    placeholder: e.options.placeholder || "请输入" + e.configs.displayName,
    modelValue: e.model,
    "onUpdate:modelValue": t[5] || (t[5] = (p) => e.model = p),
    clearable: "",
    rows: 4,
    type: e.options.type || "text",
    disabled: e.options.disabled === "true"
  }, e.$attrs), null, 16, ["style", "prefix-icon", "placeholder", "modelValue", "type", "disabled"]));
}
const Be = /* @__PURE__ */ D(Wt, [["render", Gt]]), ze = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Be
}, Symbol.toStringTag, { value: "Module" })), Yt = k({
  name: "NormalTable",
  description: "常规表格封装，使用的地方的父级必须指定高度",
  props: {
    // 表格列配置
    columns: {
      type: Array,
      default: []
    },
    // 表格数据
    tableData: {
      type: Array,
      default: []
    },
    // 是否垂直展示数据
    vertical: {
      type: Boolean,
      default: !1
    },
    // 是否树状结构数据
    isTree: {
      type: Boolean,
      default: !1
    },
    // 树形结构key值
    treeKey: {
      type: String,
      require: !1
    },
    // 是否设置了改变表头颜色
    changeHeader: {
      type: Boolean,
      default: !1
    },
    // 表格最大高度需要减去的高度，这个值不能太低，否则表格高度会自己不断拉长
    height: {
      type: String,
      default: "calc(100% - 170px)"
    },
    /**
     *  表格height属性，与height组合，可达到不同效果。
       1、height设置100%，表格没有数据时也能撑开一定高度，此时tableHeight设置100%，
        表格高度超过height时自动出现滚动条
       2、tableHeight设置为null，表格内容自动撑开高度
     */
    tableHeight: {
      type: String,
      default: "100%"
    },
    // 是否显示勾选框
    selection: {
      type: Boolean,
      default: !0
    },
    showIndex: {
      type: Boolean,
      default: !1
    },
    // 显示标题
    showHeader: {
      type: Boolean,
      default: !0
    }
  },
  emits: ["operator"],
  data() {
    return {};
  },
  computed: {
    normalHeight() {
      return {
        // height: `calc(100% - ${this.height}px)`
        height: this.height
      };
    }
  },
  watch: {
    // 表格数据变化时重新渲染表格
    tableData() {
      let t = this.$refs.table;
      t && setTimeout(() => {
        t.doLayout();
      }, 1e3);
    }
  },
  created() {
  },
  mounted() {
    window.addEventListener("resize", this.resize);
  },
  beforeDestroy() {
    window.removeEventListener("resize", this.resize);
  },
  methods: {
    changeHeaderClass(e) {
      if (this.changeHeader) {
        let t = {};
        return this.columns.forEach((l) => {
          t[l.label] = l.color || "#fff";
        }), {
          backgroundColor: t[e.column.label] || "#fff",
          color: "#000"
        };
      }
      return {
        backgroundColor: "#f6f6f6"
        // color: '#333333',
        // fontWeight: 'normal'
      };
    },
    rowChangeStyle(e) {
      if (e.row.childTransferObject && e.row.childTransferObject.length > 0 && this.isTree)
        return {
          backgroundColor: "#ddebf7"
        };
    },
    getUrl(e, t) {
      const l = /{(\w+)}/g;
      return e.cellUrl.replace(l, (a, n) => t[n]);
    },
    handler(e, t) {
      this.$emit("operator", e, t.row);
    },
    operator(e, t) {
      let l = null;
      return this.$emit("operator", e, t, (a) => {
        l = a;
      }), l;
    },
    resize() {
      this.$refs.table.doLayout();
    }
  }
});
const Jt = { style: { display: "inline-flex" } }, Zt = ["href"], Qt = { key: 3 }, Xt = { key: 0 }, el = { key: 0 }, tl = { key: 1 }, ll = ["onClick"];
function al(e, t, l, a, n, o) {
  const r = m("el-table-column"), c = m("el-tooltip"), _ = m("el-switch"), b = m("el-button"), $ = m("el-table");
  return s(), h("div", {
    class: "table-container",
    style: M(e.normalHeight)
  }, [
    i($, O({
      data: e.tableData,
      "header-cell-style": e.changeHeaderClass,
      stripe: "",
      border: "",
      height: e.tableHeight,
      ref: "table"
    }, e.$attrs), {
      default: u(() => [
        e.selection ? (s(), v(r, {
          key: 0,
          type: "selection",
          width: "40"
        })) : T("", !0),
        e.showIndex ? (s(), v(r, {
          key: 1,
          align: "center",
          label: "序号",
          type: "index",
          width: "50"
        })) : T("", !0),
        e.vertical ? T("", !0) : (s(!0), h(S, { key: 2 }, U(e.columns, (y, p) => (s(), h(S, null, [
          y.showInList && !y.hidden ? (s(), v(r, {
            align: "center",
            fixed: y.actionList ? "right" : !1,
            key: p,
            label: y.displayName,
            prop: y.name,
            resizable: "",
            sortable: y.isDataObjectField,
            width: y.width
          }, {
            header: u(() => [
              d("div", Jt, [
                d("span", null, w(y.displayName), 1),
                y.description && y.displayName != y.description ? (s(), v(c, {
                  key: 0,
                  content: y.description
                }, {
                  default: u(() => [
                    d("i", {
                      class: "el-icon-warning-outline",
                      onClick: t[0] || (t[0] = (f) => {
                        f.stopPropagation();
                      })
                    })
                  ]),
                  _: 2
                }, 1032, ["content"])) : T("", !0)
              ])
            ]),
            default: u((f) => [
              W(e.$slots, "col-" + f.column.property, {
                colData: y,
                colScope: f
              }, () => [
                y.dataType === "Boolean" ? (s(), v(_, {
                  key: 0,
                  value: f.row[y.name],
                  "active-color": "#13ce66",
                  "inactive-color": "#ff4949"
                }, null, 8, ["value"])) : !y.isDataObjectField && y.cellUrl ? (s(), h("a", {
                  key: 1,
                  href: e.getUrl(y, f.row)
                }, w(y.displayName), 9, Zt)) : y.actionList ? (s(!0), h(S, { key: 2 }, U(y.actionList, (g, C) => (s(), h(S, null, [
                  e.operator(
                    { action: "hasPermission" },
                    g.permission
                  ) ? (s(), v(b, {
                    key: C,
                    type: g.type,
                    size: "mini",
                    onClick: (F) => e.operator(g, f.row)
                  }, {
                    default: u(() => [
                      I(w(g.text), 1)
                    ]),
                    _: 2
                  }, 1032, ["type", "onClick"])) : T("", !0)
                ], 64))), 256)) : (s(), h("div", Qt, w(f.row[y.name]), 1))
              ], !0)
            ]),
            _: 2
          }, 1032, ["fixed", "label", "prop", "sortable", "width"])) : T("", !0)
        ], 64))), 256)),
        e.vertical ? (s(), v(r, {
          key: 3,
          label: "",
          width: "150px"
        }, {
          default: u(() => [
            (s(!0), h(S, null, U(e.columns, (y, p) => (s(), h(S, null, [
              y.showInList && !y.hidden ? (s(), h("div", { key: p }, [
                d("span", null, w(y.displayName), 1)
              ])) : T("", !0)
            ], 64))), 256))
          ]),
          _: 1
        })) : T("", !0),
        e.vertical ? (s(), v(r, {
          key: 4,
          label: ""
        }, {
          default: u((y) => [
            (s(!0), h(S, null, U(e.columns, (p, f) => (s(), h("div", { key: f }, [
              p.showInList && !p.hidden ? (s(), h("div", Xt, [
                p.actionList ? T("", !0) : (s(), h("span", el, w(y.row[p.name] || " "), 1)),
                p.actionList ? (s(), h("div", tl, [
                  (s(!0), h(S, null, U(p.actionList, (g, C) => (s(), h("span", {
                    class: "handbtn",
                    onClick: (F) => e.operator(g, y.row),
                    key: C
                  }, w(g.text), 9, ll))), 128))
                ])) : T("", !0)
              ])) : T("", !0)
            ]))), 128))
          ]),
          _: 1
        })) : T("", !0)
      ]),
      _: 3
    }, 16, ["data", "header-cell-style", "height"])
  ], 4);
}
const nl = /* @__PURE__ */ D(Yt, [["render", al], ["__scopeId", "data-v-f845c3c9"]]), qe = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: nl
}, Symbol.toStringTag, { value: "Module" }));
const ol = {
  props: {
    columns: {
      type: Array,
      default: () => []
    },
    operatorList: {
      type: Array,
      default: () => []
    },
    permissionFlags: {
      type: Object,
      default: () => {
      }
    }
  },
  emits: ["operator"],
  //   setup(props, context) {
  //     // console.log(arguments)
  //     const operator = (option) => {
  //       context.emit('operator', option)
  //     }
  //     return {
  //       operator
  //     }
  //   },
  data() {
    return {
      allChoose: !0,
      isIndeterminate: !1
    };
  },
  methods: {
    checkChoose() {
      let e = this.columns.filter((t) => !t.hidden).length;
      if (e == this.columns.length) {
        this.allChoose = !0;
        return;
      }
      this.allChoose = !1, this.isIndeterminate = e > 0 && e < this.columns.length;
    },
    chooseAll(e) {
      this.columns.forEach((t) => {
        t.hidden = !e;
      });
    },
    chooseItem(e) {
      e.hidden = !e.hidden, this.checkChoose();
    },
    operator(e, t) {
      this.$emit("operator", e, t);
    }
  }
}, rl = (e) => (B("data-v-e342b7ba"), e = e(), z(), e), sl = /* @__PURE__ */ rl(() => /* @__PURE__ */ d("div", null, "设置列字段", -1)), il = { class: "setting-btn" }, ul = { style: { "padding-top": "5px" } }, dl = { style: { height: "68vh", overflow: "auto" } };
function cl(e, t, l, a, n, o) {
  const r = m("el-button"), c = m("el-col"), _ = m("refresh"), b = m("el-icon"), $ = m("el-tooltip"), y = m("el-checkbox"), p = m("setting"), f = m("el-popover"), g = m("el-row");
  return s(), v(g, {
    type: "flex",
    justify: "center",
    align: "center",
    class: "operator"
  }, {
    default: u(() => [
      i(c, {
        span: 12,
        class: "left-search"
      }, {
        default: u(() => [
          (s(!0), h(S, null, U(l.operatorList, (C, F) => (s(), v(r, {
            size: "small",
            key: F,
            onClick: (V) => o.operator(C),
            type: C.type,
            plain: C.plain
          }, {
            default: u(() => [
              I(w(C.name), 1)
            ]),
            _: 2
          }, 1032, ["onClick", "type", "plain"]))), 128))
        ]),
        _: 1
      }),
      i(c, {
        span: 12,
        style: { display: "flex", "justify-content": "flex-end", "align-items": "center" }
      }, {
        default: u(() => [
          i($, {
            effect: "dark",
            content: "刷新",
            placement: "top-end"
          }, {
            default: u(() => [
              i(b, {
                class: "action",
                onClick: t[0] || (t[0] = (C) => o.operator({ action: "getTableData" }))
              }, {
                default: u(() => [
                  i(_)
                ]),
                _: 1
              })
            ]),
            _: 1
          }),
          i(f, {
            placement: "bottom",
            width: 220,
            trigger: "click"
          }, {
            reference: u(() => [
              i(b, { class: "action" }, {
                default: u(() => [
                  i(p)
                ]),
                _: 1
              })
            ]),
            default: u(() => [
              sl,
              d("div", il, [
                d("div", ul, [
                  i(y, {
                    onChange: o.chooseAll,
                    modelValue: n.allChoose,
                    "onUpdate:modelValue": t[1] || (t[1] = (C) => n.allChoose = C),
                    indeterminate: n.isIndeterminate
                  }, {
                    default: u(() => [
                      I(" 全选 ")
                    ]),
                    _: 1
                  }, 8, ["onChange", "modelValue", "indeterminate"])
                ])
              ]),
              d("div", dl, [
                (s(!0), h(S, null, U(l.columns, (C, F) => (s(), h("div", { key: F }, [
                  C.showInList ? (s(), v(y, {
                    key: 0,
                    onChange: (V) => o.chooseItem(C),
                    "model-value": !C.hidden
                  }, {
                    default: u(() => [
                      I(w(C.displayName), 1)
                    ]),
                    _: 2
                  }, 1032, ["onChange", "model-value"])) : T("", !0)
                ]))), 128))
              ])
            ]),
            _: 1
          })
        ]),
        _: 1
      })
    ]),
    _: 1
  });
}
const ml = /* @__PURE__ */ D(ol, [["render", cl], ["__scopeId", "data-v-e342b7ba"]]), He = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ml
}, Symbol.toStringTag, { value: "Module" })), pl = k({
  name: "TableSearch",
  components: {
    FormControl: Be
  },
  props: {
    columns: {
      type: Array,
      default: () => []
    },
    modelValue: {
      type: Object,
      default: () => {
      }
    },
    searchMethod: {
      type: Function,
      default: void 0
    },
    resetSearchMethod: {
      type: Function,
      default: void 0
    }
  },
  emits: ["getDataList", "resetSearch", "update:modelValue"],
  data() {
    return {
      model: {}
    };
  },
  watch: {
    model: {
      handler(e) {
        this.$emit("update:modelValue", e);
      },
      deep: !0
    },
    modelValue(e) {
      this.model = e;
    }
  },
  created() {
    this.model = this.modelValue;
    let e = this.columns;
    for (const t in e)
      if (Object.prototype.hasOwnProperty.call(e, t)) {
        const l = e[t];
        if (l.itemType === "datePicker" && l.options && l.options.includes("type=daterange") && !l.options.includes("setDefaultValue=false")) {
          const a = new Date(), n = new Date();
          n.setTime(n.getTime() - 3600 * 1e3 * 24 * 30), this.model[l.name] = [
            // 此格式化可能不同浏览器表现不同，返回的可能不是YYYY-MM-DD格式
            n.toLocaleDateString("fr-CA"),
            a.toLocaleDateString("fr-CA")
          ];
        }
        typeof l.value < "u" && (this.model[l.name] = l.value);
      }
  },
  methods: {
    search() {
      let e = this;
      e.searchMethod ? e.searchMethod() : this.$emit("getDataList");
    },
    resetSearch() {
      let e = this;
      e.resetSearchMethod ? e.resetSearchMethod() : this.$emit("resetSearch");
    }
  }
});
function hl(e, t, l, a, n, o) {
  const r = m("FormControl"), c = m("el-form-item"), _ = m("el-button"), b = m("el-form"), $ = m("el-col"), y = m("el-row");
  return s(), v(y, {
    type: "flex",
    justify: "end",
    class: "search"
  }, {
    default: u(() => [
      i($, {
        span: 24,
        class: "letf-search"
      }, {
        default: u(() => [
          i(b, {
            ref: "form",
            modelValue: e.model,
            "onUpdate:modelValue": t[1] || (t[1] = (p) => e.model = p),
            "label-position": "right",
            inline: !0,
            class: "search-form-container"
          }, {
            default: u(() => [
              (s(!0), h(S, null, U(e.columns, (p) => (s(), h(S, null, [
                p.showInSearch ? A((s(), v(c, {
                  label: p.displayName,
                  key: p.name
                }, {
                  default: u(() => [
                    !p.if || p.if(e.model) ? W(e.$slots, "search-" + p.name, {
                      key: 0,
                      model: e.model,
                      config: p
                    }, () => [
                      i(r, {
                        modelValue: e.model,
                        "onUpdate:modelValue": t[0] || (t[0] = (f) => e.model = f),
                        configs: p
                      }, null, 8, ["modelValue", "configs"])
                    ], !0) : T("", !0)
                  ]),
                  _: 2
                }, 1032, ["label"])), [
                  [N, !p.hidden]
                ]) : T("", !0)
              ], 64))), 256)),
              i(_, {
                size: "default",
                type: "primary",
                onClick: e.search
              }, {
                default: u(() => [
                  I(" 查询 ")
                ]),
                _: 1
              }, 8, ["onClick"]),
              i(_, {
                size: "default",
                type: "default",
                onClick: e.resetSearch
              }, {
                default: u(() => [
                  I(" 重置 ")
                ]),
                _: 1
              }, 8, ["onClick"])
            ]),
            _: 3
          }, 8, ["modelValue"])
        ]),
        _: 3
      })
    ]),
    _: 3
  });
}
const fl = /* @__PURE__ */ D(pl, [["render", hl], ["__scopeId", "data-v-30130dd5"]]), xe = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: fl
}, Symbol.toStringTag, { value: "Module" }));
var Y = typeof globalThis < "u" ? globalThis : typeof window < "u" ? window : typeof global < "u" ? global : typeof self < "u" ? self : {};
function gl(e) {
  return e && e.__esModule && Object.prototype.hasOwnProperty.call(e, "default") ? e.default : e;
}
var Ke = {};
(function(e) {
  Object.defineProperty(e, "__esModule", { value: !0 });
  var t = {
    name: "zh-cn",
    el: {
      colorpicker: {
        confirm: "确定",
        clear: "清空"
      },
      datepicker: {
        now: "此刻",
        today: "今天",
        cancel: "取消",
        clear: "清空",
        confirm: "确定",
        selectDate: "选择日期",
        selectTime: "选择时间",
        startDate: "开始日期",
        startTime: "开始时间",
        endDate: "结束日期",
        endTime: "结束时间",
        prevYear: "前一年",
        nextYear: "后一年",
        prevMonth: "上个月",
        nextMonth: "下个月",
        year: "年",
        month1: "1 月",
        month2: "2 月",
        month3: "3 月",
        month4: "4 月",
        month5: "5 月",
        month6: "6 月",
        month7: "7 月",
        month8: "8 月",
        month9: "9 月",
        month10: "10 月",
        month11: "11 月",
        month12: "12 月",
        weeks: {
          sun: "日",
          mon: "一",
          tue: "二",
          wed: "三",
          thu: "四",
          fri: "五",
          sat: "六"
        },
        months: {
          jan: "一月",
          feb: "二月",
          mar: "三月",
          apr: "四月",
          may: "五月",
          jun: "六月",
          jul: "七月",
          aug: "八月",
          sep: "九月",
          oct: "十月",
          nov: "十一月",
          dec: "十二月"
        }
      },
      select: {
        loading: "加载中",
        noMatch: "无匹配数据",
        noData: "无数据",
        placeholder: "请选择"
      },
      cascader: {
        noMatch: "无匹配数据",
        loading: "加载中",
        placeholder: "请选择",
        noData: "暂无数据"
      },
      pagination: {
        goto: "前往",
        pagesize: "条/页",
        total: "共 {total} 条",
        pageClassifier: "页",
        deprecationWarning: "你使用了一些已被废弃的用法，请参考 el-pagination 的官方文档"
      },
      messagebox: {
        title: "提示",
        confirm: "确定",
        cancel: "取消",
        error: "输入的数据不合法!"
      },
      upload: {
        deleteTip: "按 delete 键可删除",
        delete: "删除",
        preview: "查看图片",
        continue: "继续上传"
      },
      table: {
        emptyText: "暂无数据",
        confirmFilter: "筛选",
        resetFilter: "重置",
        clearFilter: "全部",
        sumText: "合计"
      },
      tree: {
        emptyText: "暂无数据"
      },
      transfer: {
        noMatch: "无匹配数据",
        noData: "无数据",
        titles: ["列表 1", "列表 2"],
        filterPlaceholder: "请输入搜索内容",
        noCheckedFormat: "共 {total} 项",
        hasCheckedFormat: "已选 {checked}/{total} 项"
      },
      image: {
        error: "加载失败"
      },
      pageHeader: {
        title: "返回"
      },
      popconfirm: {
        confirmButtonText: "确定",
        cancelButtonText: "取消"
      }
    }
  };
  e.default = t;
})(Ke);
const _l = /* @__PURE__ */ gl(Ke), bl = k({
  name: "App",
  components: {
    ElConfigProvider: Bt
  },
  data() {
    return {
      locale: _l
    };
  }
});
function yl(e, t, l, a, n, o) {
  const r = m("router-view"), c = m("el-config-provider");
  return s(), v(c, { locale: e.locale }, {
    default: u(() => [
      i(r)
    ]),
    _: 1
  }, 8, ["locale"]);
}
const vl = /* @__PURE__ */ D(bl, [["render", yl]]), wl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: vl
}, Symbol.toStringTag, { value: "Module" })), Il = k({
  name: "NormalTable",
  description: "常规表格封装，使用的地方的父级必须指定高度",
  props: {
    // 表格列配置
    columns: {
      type: Array,
      default: []
    },
    // 表格数据
    tableData: {
      type: Array,
      default: []
    },
    // 是否垂直展示数据
    vertical: {
      type: Boolean,
      default: !1
    },
    // 是否树状结构数据
    isTree: {
      type: Boolean,
      default: !1
    },
    // 树形结构key值
    treeKey: {
      type: String,
      require: !1
    },
    // 是否设置了改变表头颜色
    changeHeader: {
      type: Boolean,
      default: !1
    },
    // 表格最大高度需要减去的高度，这个值不能太低，否则表格高度会自己不断拉长
    normalHeight: {
      type: String,
      default: "calc(100% - 146px)"
    },
    /**
     *  表格height属性，与normalHeight组合，可达到不同效果。
       1、normalHeight设置100%，表格没有数据时也能撑开一定高度，此时height设置100%，
        表格高度超过height时自动出现滚动条
       2、height设置为null，表格内容自动撑开高度
     */
    height: {
      type: String,
      default: "100%"
    },
    // 是否显示勾选框
    selection: {
      type: Boolean,
      default: !0
    },
    // 是否显示序号列
    showIndex: {
      type: Boolean,
      default: !1
    },
    // 是否自适应列宽
    isAdjustColumnWidth: {
      type: Boolean,
      default: !1
    }
  },
  emits: ["handlerClick"],
  data() {
    return {};
  },
  // computed: {
  //   normalHeight() {
  //     // console.log('normalHeight', this.height)
  //     return {
  //       // height: `calc(100% - ${this.height}px)`
  //       height: this.tableHeight
  //     }
  //   }
  // },
  watch: {
    // 表格数据变化时重新渲染表格
    tableData() {
      const e = this, t = e.$refs.table;
      t && (setTimeout(() => {
        t.doLayout();
      }, 600), this.$nextTick(() => {
        e.isAdjustColumnWidth && e.adjustColumnWidth();
      }));
    }
  },
  created() {
  },
  mounted() {
    window.addEventListener("resize", this.resize);
  },
  beforeDestroy() {
    window.removeEventListener("resize", this.resize);
  },
  methods: {
    adjustColumnWidth() {
      if (this.$refs.table.$el)
        for (let l = 0; l < this.columns.length; l++) {
          const a = this.columns[l], n = this.getMaxWidth(l);
          n > 0 && (a.width = n + 20);
        }
    },
    getMaxWidth(e) {
      const l = this.$refs.table.$el;
      let a = 0;
      const n = l.querySelectorAll(".column" + e);
      for (const o of n)
        o.offsetWidth > a && (a = o.offsetWidth);
      return a;
    },
    changeHeaderClass(e) {
      if (this.changeHeader) {
        const t = {};
        return this.columns.forEach((l) => {
          t[l.name] = l.color || "#fff";
        }), {
          backgroundColor: t[e.column.property] || "#fff",
          color: "#000"
        };
      }
      return {
        backgroundColor: "#f6f6f6"
        // color: '#333333',
        // fontWeight: 'normal'
      };
    },
    rowChangeStyle(e) {
      if (e.row.childTransferObject && e.row.childTransferObject.length > 0 && this.isTree)
        return {
          backgroundColor: "#ddebf7"
        };
    },
    getUrl(e, t) {
      const l = /{(\w+)}/g;
      return e.cellUrl.replace(l, (a, n) => t[n]);
    },
    handlerClick(e, t) {
      let l = null;
      return this.$emit("handlerClick", e, t, (a) => {
        l = a;
      }), l;
    },
    resize() {
      const t = this.$refs.table;
      t && setTimeout(() => {
        t.doLayout();
      }, 1e3);
    },
    // 从配置中读取要显示的值，适用于下拉框
    getColumnValue(e, t) {
      if (e.$index < 0)
        return e.row[t.name];
      const l = e.row;
      let a = t.dataList;
      const n = t.url;
      if (!a)
        if (typeof n == "string" && n.startsWith("["))
          a = t.dataList = JSON.parse(n);
        else {
          if (typeof n == "string" && (n.startsWith("/") || n.startsWith("http")))
            return this.getRemoteData(l, t), l[t.name];
          if (typeof n == "object" && n.length > 0)
            a = t.dataList = t.url;
          else
            return l[t.name];
        }
      if (!a)
        return l[t.name];
      if (a.length > 0) {
        for (const o of a)
          if (o[t.valueField || "value"] === l[t.name])
            return o[t.labelField || "label"];
      }
      return l[t.name];
    },
    // 获取远程数据
    getRemoteData(e, t) {
      const l = this;
      if (t.loading)
        return;
      t.loading = !0;
      const a = t.url, n = {}, o = new URLSearchParams(t.options);
      for (const b of o)
        n[b[0]] = b[1];
      const r = n.method || "post";
      let c = {};
      this.setData(t, n, e), t.data && (c = { ...c, ...JSON.parse(t.data) });
      const _ = {
        url: a,
        method: r,
        data: c
      };
      l.$http(_).then((b) => {
        t.dataList = b.data.list || b.data.rows || b.data, t.loading = !1;
        const $ = l.$refs.table;
        $ && setTimeout(() => {
          $.doLayout();
        }, 100);
      });
    },
    // 设置请求参数，configs.data。对options.data进行处理，如果值有{{field}}形式的值，则替换成为model中的值
    setData(e, t, l) {
      if (!t.data)
        return;
      const a = JSON.parse(t.data);
      e.data || (e.data = {});
      for (const n in a)
        if (Object.prototype.hasOwnProperty.call(a, n)) {
          const o = a[n];
          typeof o == "string" && o.startsWith("{{") && o.endsWith("}}") ? l && (e.data[n] = l[o.substring(2, o.length - 2)]) : e.data[n] = o;
        }
    }
  }
});
const Tl = { style: { display: "inline-flex" } }, $l = ["data-index"], Sl = ["href"];
function Cl(e, t, l, a, n, o) {
  const r = m("el-table-column"), c = m("InfoFilled"), _ = m("el-icon"), b = m("el-tooltip"), $ = m("el-switch"), y = m("el-button"), p = m("el-table");
  return s(), h("div", {
    class: "table-container",
    style: M({ height: e.normalHeight })
  }, [
    i(p, O({
      data: e.tableData,
      "header-cell-style": e.changeHeaderClass,
      stripe: "",
      border: "",
      height: e.height,
      ref: "table"
    }, e.$attrs), {
      default: u(() => [
        e.selection ? (s(), v(r, {
          key: 0,
          align: "center",
          type: "selection",
          width: "40px"
        })) : T("", !0),
        e.showIndex ? (s(), v(r, {
          key: 1,
          align: "center",
          label: "序号",
          type: "index",
          width: "50px"
        })) : T("", !0),
        (s(!0), h(S, null, U(e.columns, (f, g) => (s(), h(S, null, [
          f.showInList && !f.hidden && (!f.if || f.if()) ? (s(), v(r, {
            align: "center",
            fixed: f.handlerList ? "right" : !1,
            key: g,
            label: f.displayName,
            prop: f.name,
            resizable: "",
            sortable: f.isDataObjectField,
            width: f.width
          }, {
            header: u(() => [
              d("div", Tl, [
                d("span", null, w(f.displayName), 1),
                f.description && f.displayName != f.description ? (s(), v(b, {
                  key: 0,
                  content: f.description
                }, {
                  default: u(() => [
                    i(_, null, {
                      default: u(() => [
                        i(c)
                      ]),
                      _: 1
                    })
                  ]),
                  _: 2
                }, 1032, ["content"])) : T("", !0)
              ])
            ]),
            default: u((C) => [
              d("span", {
                class: P(
                  "column" + g + (e.isAdjustColumnWidth ? " adjust-column-width" : "")
                ),
                "data-index": g
              }, [
                W(e.$slots, "col-" + C.column.property, {
                  colData: f,
                  colScope: C
                }, () => [
                  f.dataType === "Boolean" ? (s(), v($, {
                    key: 0,
                    modelValue: C.row[f.name],
                    "onUpdate:modelValue": (F) => C.row[f.name] = F,
                    "active-color": "#13ce66",
                    "inactive-color": "#ff4949",
                    disabled: ""
                  }, null, 8, ["modelValue", "onUpdate:modelValue"])) : f.itemType === "select" ? (s(), h(S, { key: 1 }, [
                    I(w(e.getColumnValue(C, f)), 1)
                  ], 64)) : !f.isDataObjectField && f.cellUrl ? (s(), h("a", {
                    key: 2,
                    href: e.getUrl(f, C.row)
                  }, w(f.displayName), 9, Sl)) : f.handlerList ? (s(!0), h(S, { key: 3 }, U(f.handlerList, (F, V) => (s(), h(S, { key: V }, [
                    !F.if || F.if(C.row) ? (s(), v(y, O({
                      key: 0,
                      type: F.type,
                      size: "default",
                      onClick: (ye) => e.handlerClick(F, C)
                    }, F), {
                      default: u(() => [
                        I(w(F.text), 1)
                      ]),
                      _: 2
                    }, 1040, ["type", "onClick"])) : T("", !0)
                  ], 64))), 128)) : (s(), h(S, { key: 4 }, [
                    I(w(C.row[f.name]), 1)
                  ], 64))
                ], !0)
              ], 10, $l)
            ]),
            _: 2
          }, 1032, ["fixed", "label", "prop", "sortable", "width"])) : T("", !0)
        ], 64))), 256))
      ]),
      _: 3
    }, 16, ["data", "header-cell-style", "height"])
  ], 4);
}
const We = /* @__PURE__ */ D(Il, [["render", Cl], ["__scopeId", "data-v-64da176b"]]), Fl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: We
}, Symbol.toStringTag, { value: "Module" })), Dl = k({
  name: "TableHandler",
  props: {
    columns: {
      type: Array,
      default: () => []
    },
    tableHandlerList: {
      type: Array,
      default: () => []
    },
    permissionFlags: {
      type: Object,
      default: () => {
      }
    },
    // 搜索参数
    searchParams: {
      type: Object,
      default: () => {
      }
    }
  },
  emits: ["handlerClick"],
  //   setup(props, context) {
  //     // console.log(arguments)
  //     const operator = (option) => {
  //       context.emit('operator', option)
  //     }
  //     return {
  //       operator
  //     }
  //   },
  data() {
    return {
      allChoose: !0,
      isIndeterminate: !1
    };
  },
  methods: {
    checkChoose() {
      let e = this.columns.filter((t) => !t.hidden).length;
      if (e == this.columns.length) {
        this.allChoose = !0, this.isIndeterminate = !1;
        return;
      }
      this.allChoose = !1, this.isIndeterminate = e > 0 && e < this.columns.length;
    },
    chooseAll(e) {
      this.columns.forEach((t) => {
        t.hidden = !e;
      }), e && (this.isIndeterminate = !1);
    },
    chooseItem(e) {
      e.hidden = !e.hidden, this.checkChoose();
    },
    handlerClick(e, t) {
      this.$emit("handlerClick", e, t);
    }
  }
});
const kl = (e) => (B("data-v-4d611f01"), e = e(), z(), e), Ul = /* @__PURE__ */ kl(() => /* @__PURE__ */ d("div", null, "设置列字段", -1)), Ol = { class: "setting-btn" }, El = { style: { "padding-top": "5px" } }, Al = { style: { "max-height": "380px", overflow: "auto" } };
function Pl(e, t, l, a, n, o) {
  const r = m("el-button"), c = m("el-col"), _ = m("refresh"), b = m("el-icon"), $ = m("el-tooltip"), y = m("el-checkbox"), p = m("el-divider"), f = m("setting"), g = m("el-popover"), C = m("el-row");
  return s(), v(C, {
    type: "flex",
    justify: "center",
    align: "middle",
    class: "operator"
  }, {
    default: u(() => [
      i(c, {
        span: 12,
        class: "left-search"
      }, {
        default: u(() => [
          (s(!0), h(S, null, U(e.tableHandlerList, (F, V) => (s(), h(S, { key: V }, [
            !F.if || F.if(e.searchParams) ? W(e.$slots, "handler-" + F.name, {
              key: 0,
              config: F
            }, () => [
              i(r, O({
                size: "default",
                onClick: (ye) => e.handlerClick(F)
              }, F), {
                default: u(() => [
                  I(w(F.name), 1)
                ]),
                _: 2
              }, 1040, ["onClick"])
            ], !0) : T("", !0)
          ], 64))), 128))
        ]),
        _: 3
      }),
      i(c, {
        span: 12,
        style: { display: "flex", "justify-content": "flex-end", "align-items": "center" }
      }, {
        default: u(() => [
          i($, {
            effect: "dark",
            content: "刷新",
            placement: "top-end"
          }, {
            default: u(() => [
              i(b, {
                class: "action",
                onClick: t[0] || (t[0] = (F) => e.handlerClick({ handler: "getDataList" }))
              }, {
                default: u(() => [
                  i(_)
                ]),
                _: 1
              })
            ]),
            _: 1
          }),
          i(g, {
            placement: "bottom",
            width: 220,
            trigger: "click"
          }, {
            reference: u(() => [
              d("div", null, [
                i($, {
                  effect: "dark",
                  content: "列设置",
                  placement: "top-end"
                }, {
                  default: u(() => [
                    i(b, { class: "action" }, {
                      default: u(() => [
                        i(f)
                      ]),
                      _: 1
                    })
                  ]),
                  _: 1
                })
              ])
            ]),
            default: u(() => [
              Ul,
              d("div", Ol, [
                d("div", El, [
                  i(y, {
                    onChange: e.chooseAll,
                    modelValue: e.allChoose,
                    "onUpdate:modelValue": t[1] || (t[1] = (F) => e.allChoose = F),
                    indeterminate: e.isIndeterminate
                  }, {
                    default: u(() => [
                      I(" 全选 ")
                    ]),
                    _: 1
                  }, 8, ["onChange", "modelValue", "indeterminate"])
                ])
              ]),
              i(p),
              d("div", Al, [
                (s(!0), h(S, null, U(e.columns, (F, V) => (s(), h("div", { key: V }, [
                  F.showInList ? (s(), v(y, {
                    key: 0,
                    onChange: (ye) => e.chooseItem(F),
                    "model-value": !F.hidden
                  }, {
                    default: u(() => [
                      I(w(F.displayName), 1)
                    ]),
                    _: 2
                  }, 1032, ["onChange", "model-value"])) : T("", !0)
                ]))), 128))
              ])
            ]),
            _: 1
          })
        ]),
        _: 1
      })
    ]),
    _: 3
  });
}
const Ge = /* @__PURE__ */ D(Dl, [["render", Pl], ["__scopeId", "data-v-4d611f01"]]), Vl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Ge
}, Symbol.toStringTag, { value: "Module" })), Nl = k({
  name: "FormControl",
  // components: { CustomSelect },
  props: {
    // modelValue: [String, Number, Object, Boolean, Array],
    modelValue: [Object],
    // 字段配置
    configs: {
      type: Object,
      default: {}
    }
  },
  emits: ["update:modelValue"],
  data() {
    return {
      model: void 0,
      shortcuts: [
        {
          text: "昨天",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 1), e.setTime(e.getTime() - 3600 * 1e3 * 24 * 1), [t, e];
          }
        },
        {
          text: "今天",
          value() {
            const e = new Date();
            return [new Date(), e];
          }
        },
        {
          text: "最近一周",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 7), [t, e];
          }
        },
        {
          text: "最近一个月",
          value() {
            const e = new Date(), t = new Date();
            return t.setTime(t.getTime() - 3600 * 1e3 * 24 * 30), [t, e];
          }
        }
      ],
      // 选项列表
      dataList: []
    };
  },
  computed: {
    // ui组件的配置
    options() {
      let e = this, t = {}, l = e.configs;
      if (!l.options)
        return t;
      if (typeof l.options == "object")
        return l.options;
      let a = new URLSearchParams(l.options);
      for (const n of a) {
        let o = n[1];
        t[n[0]] = o;
      }
      return t;
    },
    name() {
      return this.configs.name;
    },
    selectOptions() {
      return this.dataList.map(
        (e) => new Object({
          label: e[this.options.labelField || "label"],
          // 如果是多选，由于数组转逗号隔开的字符串，再转成数组，值就变成字符串，因此统一处理为字符串
          value: this.options.multiple === "true" || this.options.multiple === !0 ? this.getValueByDataType(e, this.options) + "" : this.getValueByDataType(e, this.options)
        })
      );
    }
  },
  watch: {
    model(e, t) {
      let l = this.name, a = this.configs, n = this.options, o = this.modelValue;
      if (l.includes("$")) {
        let r = e, c = l.split("$");
        a.itemType === "datePicker" && a.options && (a.options.type === "daterange" || typeof a.options == "string" && a.options.includes("type=daterange")) ? this.valueValidate(r) ? (o[c[0]] = r[0], o[c[1]] = r[1]) : (o[c[0]] = void 0, o[c[1]] = void 0) : a.itemType === "checkbox" ? this.valueValidate(r) ? o[c[0]] = r.join() : o[c[0]] = void 0 : a.itemType === "select" && (n.multiple == "true" || n.multiple === !0) ? this.valueValidate(r) ? o[c[0]] = r.join() : o[c[0]] = void 0 : console.warn("表单名中带$，但是未配置处理器");
      } else
        a.itemType === "select" && !this.valueValidate(e) && (e = void 0), o[l] = e;
    },
    // 外部修改此值，以便传递到model
    modelValue: {
      handler(e, t) {
        const l = this.name;
        let a = e[this.name];
        const n = this.configs, o = this.options;
        if (l.includes("$")) {
          const r = l.split("$");
          if (n.itemType === "datePicker" && n.options && (n.options.type === "daterange" || n.options.toString().includes("type=daterange"))) {
            const c = e[r[0]], _ = e[r[1]];
            this.valueValidate(c) && this.valueValidate(_) ? this.model = [c, _] : (!this.modelValue[r[0]] || !this.modelValue[r[1]]) && (this.model = void 0);
          } else if (n.itemType === "checkbox")
            if (a = e[r[0]], this.valueValidate(a)) {
              const c = a.split(",");
              this.model = c;
            } else
              this.model = void 0;
          else if (n.itemType === "select" && (o.multiple === "true" || o.multiple === !0))
            if (a = e[r[0]], this.valueValidate(a)) {
              const c = a.split(",");
              this.model = c;
            } else
              this.model = [];
        } else
          n.itemType === "select" && (o.multiple === "true" || o.multiple === !0) ? this.model = a || [] : this.model = a;
      },
      // 加了此选项，就不用再created赋值
      immediate: !0,
      deep: !0
    },
    "configs.url": {
      handler() {
        this.getData();
      }
    },
    // 获取远程数据的参数
    "configs.data": {
      handler() {
        this.getData();
      },
      deep: !0
    }
    // !!! 不能监听，外部如果共用configs，那么所有用了该configs的组件都会受影响
    // 'configs.dataList': {
    //   handler() {
    //     this.dataList = this.configs.dataList
    //   },
    //   deep: true
    // }
  },
  created() {
    this.getData();
  },
  methods: {
    // 设置请求参数，configs.data。对options.data进行处理，如果值有{{field}}形式的值，则替换成为model中的值
    setData() {
      if (!this.options.data)
        return;
      let e;
      typeof this.options.data == "object" ? e = this.options.data : e = JSON.parse(this.options.data), this.configs.data || (this.configs.data = {});
      for (const t in e)
        if (Object.prototype.hasOwnProperty.call(e, t)) {
          const l = e[t];
          typeof l == "string" && l.startsWith("{{") && l.endsWith("}}") ? this.modelValue && (this.configs.data[t] = this.modelValue[l.substring(2, l.length - 2)]) : this.configs.data[t] = l;
        }
    },
    // 类似多选下拉的组件，设置默认值
    setDefaultValue() {
      const e = this, t = e.options, l = e.modelValue;
      if (e.dataList) {
        if (!t.getValueField || !t.getLabelField)
          return;
        const a = l[t.getValueField];
        if (!a)
          return;
        const n = {
          [t.labelField]: l[t.getLabelField] || a,
          [t.valueField]: a
        };
        e.dataList.push(n);
      }
    },
    getData() {
      const e = this, t = e.configs.url;
      if (t) {
        if (e.options.remote === "true" || e.options.remote === !0) {
          this.setDefaultValue();
          return;
        }
        if (typeof t == "object" && t.length > 0) {
          e.dataList = t;
          return;
        }
        if (typeof t != "string") {
          console.warn("配置中url不正确，不进行处理", t);
          return;
        }
        t.startsWith("[") ? e.getLocalData() : t.startsWith("/") || t.startsWith("http") ? e.getRemoteData() : console.warn("配置中url不正确，不进行处理", t);
      }
    },
    // 获取远程数据
    getRemoteData(e = "") {
      const t = this, l = t.configs.url, a = t.options.method || "post";
      let o = {
        [t.options.keyField || "txtKeywords"]: e
      };
      this.setData(), t.configs.data && (o = { ...o, ...t.configs.data });
      const r = {
        url: l,
        method: a,
        data: void 0,
        params: void 0
      };
      a === "post" ? r.data = o : a === "get" && (r.params = o), t.$http(r).then((c) => {
        const _ = c.data, b = _.rows || _.list || _;
        t.dataList = typeof t.options.afterGetDataList == "function" ? t.options.afterGetDataList(b) : b;
      });
    },
    // 解析url中的数据
    getLocalData() {
      const e = this, t = JSON.parse(e.configs.url);
      e.dataList = t;
    },
    getValueByDataType(e, t) {
      let l = e[t.valueField || "value"];
      return (t.dataType === "Int32" || t.dataType === "int") && (l = parseInt(l)), l;
    },
    /**
     * 有效值验证，不为undefined、空字符串、null
     */
    valueValidate(e) {
      return e !== "" && e !== void 0 && e !== null;
    }
  }
});
function jl(e, t, l, a, n, o) {
  const r = m("el-select-v2"), c = m("el-radio"), _ = m("el-checkbox"), b = m("el-checkbox-group"), $ = m("el-date-picker"), y = m("el-switch"), p = m("el-input");
  return e.configs.itemType === "select" ? (s(), v(r, O({
    key: 0,
    modelValue: e.model,
    "onUpdate:modelValue": t[0] || (t[0] = (f) => e.model = f),
    size: "default",
    style: { width: e.configs.width ? e.configs.width : "220px" },
    placeholder: e.options.placeholder || "请选择" + e.configs.displayName,
    remote: e.options.remote === "true" || e.options.remote === !0,
    "remote-method": e.getRemoteData,
    "allow-create": e.options.allowCreate === "true" || e.options.allowCreate === !0,
    multiple: e.options.multiple === "true" || e.options.multiple === !0,
    options: e.selectOptions,
    filterable: "",
    clearable: ""
  }, e.$attrs), null, 16, ["modelValue", "style", "placeholder", "remote", "remote-method", "allow-create", "multiple", "options"])) : e.configs.itemType === "radio" ? (s(), h("div", {
    key: 1,
    style: M({ width: e.configs.width ? e.configs.width : "220px" })
  }, [
    e.valueValidate(e.options.value1) ? (s(), v(c, O({
      key: 0,
      modelValue: e.model,
      "onUpdate:modelValue": t[1] || (t[1] = (f) => e.model = f),
      label: typeof e.model == "number" ? parseInt(e.options.value1) : e.options.value1
    }, e.$attrs), {
      default: u(() => [
        I(w(e.options.label1), 1)
      ]),
      _: 1
    }, 16, ["modelValue", "label"])) : T("", !0),
    e.valueValidate(e.options.value2) ? (s(), v(c, O({
      key: 1,
      modelValue: e.model,
      "onUpdate:modelValue": t[2] || (t[2] = (f) => e.model = f),
      label: typeof e.model == "number" ? parseInt(e.options.value2) : e.options.value2
    }, e.$attrs), {
      default: u(() => [
        I(w(e.options.label2), 1)
      ]),
      _: 1
    }, 16, ["modelValue", "label"])) : T("", !0)
  ], 4)) : e.configs.itemType === "checkbox" ? (s(), h("div", {
    key: 2,
    style: M({ width: e.configs.width ? e.configs.width : "220px" })
  }, [
    i(b, O({
      modelValue: e.model,
      "onUpdate:modelValue": t[3] || (t[3] = (f) => e.model = f)
    }, e.$attrs), {
      default: u(() => [
        (s(!0), h(S, null, U(e.dataList, (f) => (s(), v(_, {
          label: e.getValueByDataType(f, e.options)
        }, {
          default: u(() => [
            I(w(f[e.options.labelField || "label"]), 1)
          ]),
          _: 2
        }, 1032, ["label"]))), 256))
      ]),
      _: 1
    }, 16, ["modelValue"])
  ], 4)) : e.configs.itemType === "datePicker" ? (s(), v($, O({
    key: 3,
    modelValue: e.model,
    "onUpdate:modelValue": t[4] || (t[4] = (f) => e.model = f),
    style: { width: e.configs.width ? e.configs.width : "220px" },
    size: "default",
    class: "date-time-picker",
    type: e.options.type,
    format: "YYYY-MM-DD",
    "value-format": "YYYY-MM-DD",
    "range-separator": "至",
    "start-placeholder": "开始时间",
    "end-placeholder": "结束时间",
    shortcuts: e.shortcuts
  }, e.$attrs), null, 16, ["modelValue", "style", "type", "shortcuts"])) : e.configs.itemType === "switch" || e.configs.dataType === "Boolean" ? (s(), v(y, {
    key: 4,
    modelValue: e.model,
    "onUpdate:modelValue": t[5] || (t[5] = (f) => e.model = f),
    style: M({ width: e.configs.width ? e.configs.width : "220px" }),
    "active-color": "#13ce66",
    "inactive-color": "#ff4949",
    disabled: e.options.disabled === "true" || e.options.disabled === !0
  }, null, 8, ["modelValue", "style", "disabled"])) : (s(), v(p, O({
    key: 5,
    size: "default",
    style: { width: e.configs.width ? e.configs.width : "220px" },
    "prefix-icon": e.options.icon,
    placeholder: e.options.placeholder || "请输入" + e.configs.displayName,
    modelValue: e.model,
    "onUpdate:modelValue": t[6] || (t[6] = (f) => e.model = f),
    clearable: "",
    autosize: "",
    rows: 4,
    type: e.options.type || "text",
    disabled: e.options.disabled === "true" || e.options.disabled === !0
  }, e.$attrs), null, 16, ["style", "prefix-icon", "placeholder", "modelValue", "type", "disabled"]));
}
const ue = /* @__PURE__ */ D(Nl, [["render", jl]]), Ll = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ue
}, Symbol.toStringTag, { value: "Module" })), Ml = k({
  name: "TableSearch",
  components: {
    FormControl: ue
  },
  props: {
    columns: {
      type: Array,
      default: () => []
    },
    modelValue: {
      type: Object,
      default: () => {
      }
    },
    searchMethod: {
      type: Function,
      default: void 0
    },
    resetSearchMethod: {
      type: Function,
      default: void 0
    }
  },
  emits: ["getDataList", "resetSearch", "update:modelValue"],
  data() {
    return {
      model: {}
    };
  },
  watch: {
    model: {
      handler(e) {
        this.$emit("update:modelValue", e);
      },
      deep: !0
    },
    modelValue(e) {
      this.model = e;
    }
  },
  created() {
    this.model = this.modelValue;
    const e = this.columns;
    for (const t in e)
      if (Object.prototype.hasOwnProperty.call(e, t)) {
        const l = e[t];
        if (l.name.includes("$") && l.itemType === "datePicker" && l.options && (l.options.type === "daterange" || typeof l.options == "string" && l.options.includes("type=daterange")) && (l.options.setDefaultValue || typeof l.options == "string" && !l.options.includes("setDefaultValue=false"))) {
          const n = l.name.split("$"), o = new Date(), r = new Date();
          r.setTime(r.getTime() - 3600 * 1e3 * 24 * 30), this.model[n[0]] = r.toLocaleDateString("fr-CA"), this.model[n[1]] = o.toLocaleDateString("fr-CA");
        }
        typeof l.value < "u" && (this.model[l.name] = l.value);
      }
  },
  methods: {
    search() {
      const e = this;
      e.searchMethod ? e.searchMethod() : this.$emit("getDataList");
    },
    resetSearch() {
      const e = this;
      e.resetSearchMethod ? e.resetSearchMethod() : this.$emit("resetSearch");
    }
  }
});
function Rl(e, t, l, a, n, o) {
  const r = m("FormControl"), c = m("el-form-item"), _ = m("el-button"), b = m("el-form"), $ = m("el-col"), y = m("el-row");
  return s(), v(y, {
    type: "flex",
    justify: "end",
    class: "search"
  }, {
    default: u(() => [
      i($, {
        span: 24,
        class: "letf-search"
      }, {
        default: u(() => [
          i(b, {
            ref: "form",
            modelValue: e.model,
            "onUpdate:modelValue": t[1] || (t[1] = (p) => e.model = p),
            "label-position": "right",
            inline: !0,
            class: "search-form-container"
          }, {
            default: u(() => [
              (s(!0), h(S, null, U(e.columns, (p) => (s(), h(S, null, [
                p.showInSearch && (!p.if || p.if(e.model)) ? A((s(), v(c, {
                  label: p.displayName,
                  key: p.name
                }, {
                  default: u(() => [
                    W(e.$slots, "search-" + p.name, {
                      model: e.model,
                      config: p
                    }, () => [
                      i(r, {
                        modelValue: e.model,
                        "onUpdate:modelValue": t[0] || (t[0] = (f) => e.model = f),
                        configs: p
                      }, null, 8, ["modelValue", "configs"])
                    ], !0)
                  ]),
                  _: 2
                }, 1032, ["label"])), [
                  [N, !p.hidden]
                ]) : T("", !0)
              ], 64))), 256)),
              i(_, {
                size: "default",
                type: "primary",
                onClick: e.search
              }, {
                default: u(() => [
                  I(" 查询 ")
                ]),
                _: 1
              }, 8, ["onClick"]),
              i(_, {
                size: "default",
                type: "default",
                onClick: e.resetSearch
              }, {
                default: u(() => [
                  I(" 重置 ")
                ]),
                _: 1
              }, 8, ["onClick"])
            ]),
            _: 3
          }, 8, ["modelValue"])
        ]),
        _: 3
      })
    ]),
    _: 3
  });
}
const Ye = /* @__PURE__ */ D(Ml, [["render", Rl], ["__scopeId", "data-v-66f5e351"]]), Bl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Ye
}, Symbol.toStringTag, { value: "Module" })), zl = k({
  name: "TablePagination",
  props: ["modelValue"],
  emits: ["update:modelValue", "pagerChange"],
  watch: {
    "modelValue.pageSize": {
      handler(e, t) {
        e != t && (this.$emit("update:modelValue", this.modelValue), this.$emit("pagerChange", this.modelValue));
      },
      deep: !0
    },
    "modelValue.pageIndex": {
      handler(e, t) {
        e != t && (this.$emit("update:modelValue", this.modelValue), this.$emit("pagerChange", this.modelValue));
      },
      deep: !0
    }
  }
  // methods: {
  //   pagerChange() {
  //     this.$emit('pagerChange', this.modelValue)
  //   }
  // }
});
const ql = { class: "table-pagination" };
function Hl(e, t, l, a, n, o) {
  const r = m("el-pagination");
  return s(), h("div", ql, [
    i(r, O({
      style: { height: "46px" },
      background: "",
      "current-page": e.modelValue.pageIndex,
      "onUpdate:currentPage": t[0] || (t[0] = (c) => e.modelValue.pageIndex = c),
      "page-size": e.modelValue.pageSize,
      "onUpdate:pageSize": t[1] || (t[1] = (c) => e.modelValue.pageSize = c),
      layout: "total, prev, pager, next, sizes, jumper",
      total: e.modelValue.total
    }, e.$attrs), null, 16, ["current-page", "page-size", "total"])
  ]);
}
const Je = /* @__PURE__ */ D(zl, [["render", Hl], ["__scopeId", "data-v-d941e078"]]), xl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Je
}, Symbol.toStringTag, { value: "Module" })), Ze = k({
  name: "AdvancedTable",
  props: {
    // 搜索条件列表
    searchList: {
      type: Array,
      default: []
    },
    // 操作列表
    tableHandlerList: {
      type: Array,
      default: []
    },
    // 表格数据
    tableDataList: {
      type: Array,
      default: null
    },
    // 表格列配置
    columns: {
      type: Array,
      default: []
    },
    // 表格数据请求地址，如果设置了tableData，url参数将不会生效
    url: {
      type: String,
      require: !0,
      default: ""
    },
    // 显示搜索条件区
    showSearch: {
      type: Boolean,
      default: !0
    },
    // 显示操作区
    showHandler: {
      type: Boolean,
      default: !0
    },
    // 显示分页
    showPagination: {
      type: Boolean,
      default: !0
    },
    // 请求数据前的过滤方法
    beforeGetDataList: {
      type: Function,
      require: !1
    },
    // 请求数据后的过滤方法
    afterGetDataList: {
      type: Function,
      require: !1
    },
    // 当此组件的父级不是整个页面而是某个组件，需要设置父级，以便操作方法能被找到
    tableParent: {
      type: Object,
      require: !1
    }
  },
  data() {
    return {
      // 组件激活状态，默认true。失活时置为false，被激活时判断该值，
      // 如果为false则更新列表数据，避免第一次创建组件时激活重复获取数据
      activated: !0,
      loadingTable: !1,
      showTable: !0,
      // 批量选中的数据
      selectList: [],
      // 表格数据
      tableData: [],
      // 分页参数
      pager: {
        pageSize: 10,
        pageIndex: 1,
        total: 0,
        totalCount: 0,
        desc: !0,
        sort: void 0
      },
      // 搜索参数
      searchParams: {},
      // 搜索框高度
      tableSearchHeight: 50
    };
  },
  computed: {
    // 非表格的其它组件高度
    nonTableHeight() {
      const e = this;
      return e.tableSearchHeight + (e.showHandler ? 50 : 0) + (e.showPagination ? 50 : 0);
    },
    // 表格高度
    normalTableHeight() {
      return `calc(100% - ${this.nonTableHeight + 3}px)`;
    }
  },
  watch: {
    tableDataList(e) {
      this.tableData = this.tableDataList;
    }
  },
  mounted() {
    this.getDataList(), this.resize(), window.addEventListener("resize", this.resize);
  },
  beforeDestroy() {
    window.removeEventListener("resize", this.resize);
  },
  activated() {
    this.activated || (this.activated = !0, this.getDataList());
  },
  deactivated() {
    this.activated = !1;
  },
  methods: {
    // 获取表格数据
    getDataList() {
      if (this.tableDataList) {
        this.tableData = this.tableDataList;
        return;
      }
      const e = this.searchParams, t = {
        // limit: this.pager.pageSize,
        // offset: this.pager.pageIndex - 1,
        // currentPage: this.pager.pageIndex,
        keyWord: e.txtKeywords,
        ...this.pager,
        ...e
      };
      this.beforeGetDataList && this.beforeGetDataList(t), this.loadingTable = !0, this.$http.post(this.url, t).then((l) => {
        this.tableData = l.data.list || l.data.rows || l.data, l.data.pagerModel ? this.pager.total = l.data.pagerModel.total : l.pager ? this.pager.total = parseInt(l.pager.totalCount || 0) : this.pager.total = l.data.total || 0, this.afterGetDataList && this.afterGetDataList(l.data), this.loadingTable = !1;
      });
    },
    resetSearch() {
      this.pager = {
        pageSize: 10,
        pageIndex: 1,
        total: 0,
        totalCount: 0,
        desc: !0,
        sort: void 0
      }, this.searchParams = {}, this.getDataList();
    },
    // 子组件调用此方法，再通过参数action调用本组件方法
    handler(e, t, l) {
      const a = this, n = e.handler;
      let o;
      if (this.tableParent && (o = this.tableParent[n]), !o && this.$parent && (o = this.$parent[n]), o || (o = a[n]), !o || typeof o != "function") {
        const r = `未实现的方法：${n}，或请设置属性=>:table-parent="this"`;
        console.error(r), a.$message.error(r);
      } else {
        const r = o.call(a, t);
        typeof l == "function" && l(r);
      }
    },
    // 设置选中的行数
    setSelectList(e) {
      this.selectList = e;
    },
    // 设置显示的表头
    setColumns(e) {
    },
    resize() {
      const e = this;
      e.$nextTick(() => {
        const t = e.$refs.tableSearch;
        t ? e.tableSearchHeight = t.$el.offsetHeight : e.tableSearchHeight = 0;
      });
    },
    handlerSortChange({
      col: e,
      prop: t,
      order: l
    }) {
      console.log(e, t, l), l === "ascending" ? (this.pager.desc = !1, this.pager.sort = t) : l === "descending" ? (this.pager.desc = !0, this.pager.sort = t) : (this.pager.desc = !0, this.pager.sort = void 0), this.getDataList();
    }
  },
  render(e) {
    return i("div", {
      style: {
        height: "100%"
      }
    }, [e.showSearch ? i(Ye, {
      ref: "tableSearch",
      modelValue: e.searchParams,
      "onUpdate:modelValue": (t) => e.searchParams = t,
      columns: e.searchList,
      onGetDataList: e.getDataList,
      onResetSearch: e.resetSearch
    }, e.$slots) : "", e.showHandler ? i(Ge, {
      ref: "tableHandler",
      columns: e.columns,
      tableHandlerList: e.tableHandlerList,
      searchParams: e.searchParams,
      onHandlerClick: e.handler
    }, e.$slots) : "", e.showTable ? A(i(We, O({
      ref: "normalTable",
      columns: e.columns,
      tableData: e.tableData,
      normalHeight: e.normalTableHeight,
      onSelectionChange: e.setSelectList,
      onHandlerClick: e.handler,
      onSortChange: e.handlerSortChange
    }, e.$attrs), e.$slots), [[Re("loading"), e.loadingTable]]) : "", e.showPagination ? i(Je, {
      ref: "tablePagination",
      onPagerChange: e.getDataList,
      modelValue: e.pager,
      "onUpdate:modelValue": (t) => e.pager = t
    }, null) : ""]);
  }
}), Kl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Ze
}, Symbol.toStringTag, { value: "Module" })), Wl = k({
  name: "multipleSelect",
  props: {
    url: {
      type: String,
      required: !0
    },
    value: {
      required: !0
    }
  },
  computed: {
    kv() {
      var e = this.url.substring(this.url.lastIndexOf("?") + 1), t = {}, l = /([^?&=]+)=([^?&=]*)/g;
      return e.replace(l, function(a, n, o) {
        var r = decodeURIComponent(n), c = decodeURIComponent(o);
        return c = String(c), t[r] = c, a;
      }), t;
    }
  },
  data() {
    return {
      options: [],
      data: ""
    };
  },
  watch: {
    data(e, t) {
      this.$emit("input", e.join());
    }
  },
  methods: {
    getData() {
      let e = this;
      e.options.length > 0 || e.$http({
        url: e.url,
        method: "post"
      }).then((t) => {
        let l = t.data;
        for (let a = 0; a < l.length; a++) {
          const n = l[a];
          e.options[a] = { key: n[e.kv.key], value: n[e.kv.value] + "" };
        }
        e.$forceUpdate();
      });
    }
  }
});
function Gl(e, t, l, a, n, o) {
  const r = m("el-option"), c = m("el-select");
  return s(), v(c, {
    modelValue: e.data,
    "onUpdate:modelValue": t[0] || (t[0] = (_) => e.data = _),
    multiple: !0,
    filterable: "",
    clearable: "",
    onFocus: e.getData
  }, {
    default: u(() => [
      (s(!0), h(S, null, U(e.options, (_) => (s(), v(r, {
        key: _.value,
        label: _.key,
        value: _.value
      }, null, 8, ["label", "value"]))), 128))
    ]),
    _: 1
  }, 8, ["modelValue", "onFocus"]);
}
const Yl = /* @__PURE__ */ D(Wl, [["render", Gl]]), Jl = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Yl
}, Symbol.toStringTag, { value: "Module" })), Zl = k({
  name: "singleSelect",
  props: {
    url: {
      type: String,
      required: !0
    },
    value: {
      required: !0
    }
  },
  computed: {
    kv() {
      var e = this.url.substring(this.url.lastIndexOf("?") + 1), t = {}, l = /([^?&=]+)=([^?&=]*)/g;
      return e.replace(l, function(a, n, o) {
        var r = decodeURIComponent(n), c = decodeURIComponent(o);
        return c = String(c), t[r] = c, a;
      }), t;
    }
  },
  data() {
    return {
      options: [],
      data: ""
    };
  },
  watch: {
    data(e, t) {
      this.$emit("input", e);
    }
  },
  methods: {
    getData() {
      let e = this;
      e.url && (e.options.length > 0 || (e.url.substring(0, 1) === "[" ? e.getLocalData() : e.getRemoteData()));
    },
    getRemoteData() {
      let e = this;
      e.$http({
        url: e.url,
        method: "post"
      }).then((t) => {
        let l = t.data;
        for (let a = 0; a < l.length; a++) {
          const n = l[a];
          e.options[a] = { key: n[e.kv.key], value: n[e.kv.value] + "" };
        }
        e.$forceUpdate();
      });
    },
    getLocalData() {
      let e = this, t = JSON.parse(e.url);
      e.options = t, e.$forceUpdate();
    }
  }
});
function Ql(e, t, l, a, n, o) {
  const r = m("el-option"), c = m("el-select");
  return s(), v(c, {
    modelValue: e.data,
    "onUpdate:modelValue": t[0] || (t[0] = (_) => e.data = _),
    filterable: "",
    clearable: "",
    onFocus: e.getData
  }, {
    default: u(() => [
      (s(!0), h(S, null, U(e.options, (_) => (s(), v(r, {
        key: _.value,
        label: _.key,
        value: _.value
      }, null, 8, ["label", "value"]))), 128))
    ]),
    _: 1
  }, 8, ["modelValue", "onFocus"]);
}
const Xl = /* @__PURE__ */ D(Zl, [["render", Ql]]), ea = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Xl
}, Symbol.toStringTag, { value: "Module" })), ta = k({
  name: "AppMain",
  computed: {
    // cachedViews() {
    //   return this.$store.state.tagsView.cachedViews
    // },
    key() {
      return this.$route.fullPath;
    }
  }
});
const la = { class: "app-main" };
function aa(e, t, l, a, n, o) {
  const r = m("router-view");
  return s(), h("section", la, [
    (s(), v(r, { key: e.key }))
  ]);
}
const na = /* @__PURE__ */ D(ta, [["render", aa], ["__scopeId", "data-v-73507bf8"]]), oa = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: na
}, Symbol.toStringTag, { value: "Module" })), ra = k({
  name: "Hamburger",
  props: {
    isActive: {
      type: Boolean,
      default: !1
    },
    toggleClick: {
      type: Function,
      default: null
    }
  },
  computed: {
    show() {
      return this.$store.getters.app.device === "mobile";
    }
  }
});
const de = (e) => (B("data-v-a3992754"), e = e(), z(), e), sa = /* @__PURE__ */ de(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 568.849776 57.196677 568.849776c-31.397081 0-56.850799-25.452695-56.850799-56.850799l0 0c0-31.397081 25.452695-56.849776 56.850799-56.849776l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.849776l0 0C1023.653099 543.397081 998.200404 568.849776 966.8023 568.849776z",
  "p-id": "1692"
}, null, -1)), ia = /* @__PURE__ */ de(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 881.527125 57.196677 881.527125c-31.397081 0-56.850799-25.452695-56.850799-56.849776l0 0c0-31.397081 25.452695-56.849776 56.850799-56.849776l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.849776l0 0C1023.653099 856.07443 998.200404 881.527125 966.8023 881.527125z",
  "p-id": "1693"
}, null, -1)), ua = /* @__PURE__ */ de(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 256.17345 57.196677 256.17345c-31.397081 0-56.850799-25.452695-56.850799-56.849776l0 0c0-31.397081 25.452695-56.850799 56.850799-56.850799l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.850799l0 0C1023.653099 230.720755 998.200404 256.17345 966.8023 256.17345z",
  "p-id": "1694"
}, null, -1)), da = [
  sa,
  ia,
  ua
];
function ca(e, t, l, a, n, o) {
  return s(), h("div", null, [
    A((s(), h("svg", {
      class: P([{ "is-active": e.isActive }, "hamburger"]),
      t: "1492500959545",
      viewBox: "0 0 1024 1024",
      version: "1.1",
      xmlns: "http://www.w3.org/2000/svg",
      "p-id": "1691",
      "xmlns:xlink": "http://www.w3.org/1999/xlink",
      width: "64",
      height: "64",
      onClick: t[0] || (t[0] = (...r) => e.toggleClick && e.toggleClick(...r))
    }, da, 2)), [
      [N, e.show]
    ])
  ]);
}
const ma = /* @__PURE__ */ D(ra, [["render", ca], ["__scopeId", "data-v-a3992754"]]), pa = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ma
}, Symbol.toStringTag, { value: "Module" })), ha = k({
  name: "Hamburger",
  props: {
    isActive: {
      type: Boolean,
      default: !1
    },
    toggleClick: {
      type: Function,
      default: null
    }
  },
  computed: {
    show() {
      return this.$store.getters.app.device === "mobile";
    }
  }
});
const ce = (e) => (B("data-v-d6fb74b8"), e = e(), z(), e), fa = /* @__PURE__ */ ce(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 568.849776 57.196677 568.849776c-31.397081 0-56.850799-25.452695-56.850799-56.850799l0 0c0-31.397081 25.452695-56.849776 56.850799-56.849776l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.849776l0 0C1023.653099 543.397081 998.200404 568.849776 966.8023 568.849776z",
  "p-id": "1692"
}, null, -1)), ga = /* @__PURE__ */ ce(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 881.527125 57.196677 881.527125c-31.397081 0-56.850799-25.452695-56.850799-56.849776l0 0c0-31.397081 25.452695-56.849776 56.850799-56.849776l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.849776l0 0C1023.653099 856.07443 998.200404 881.527125 966.8023 881.527125z",
  "p-id": "1693"
}, null, -1)), _a = /* @__PURE__ */ ce(() => /* @__PURE__ */ d("path", {
  d: "M966.8023 256.17345 57.196677 256.17345c-31.397081 0-56.850799-25.452695-56.850799-56.849776l0 0c0-31.397081 25.452695-56.850799 56.850799-56.850799l909.605623 0c31.397081 0 56.849776 25.452695 56.849776 56.850799l0 0C1023.653099 230.720755 998.200404 256.17345 966.8023 256.17345z",
  "p-id": "1694"
}, null, -1)), ba = [
  fa,
  ga,
  _a
];
function ya(e, t, l, a, n, o) {
  return s(), h("div", null, [
    A((s(), h("svg", {
      class: P([{ "is-active": e.isActive }, "hamburger"]),
      t: "1492500959545",
      viewBox: "0 0 1024 1024",
      version: "1.1",
      xmlns: "http://www.w3.org/2000/svg",
      "p-id": "1691",
      "xmlns:xlink": "http://www.w3.org/1999/xlink",
      width: "64",
      height: "64",
      onClick: t[0] || (t[0] = (...r) => e.toggleClick && e.toggleClick(...r))
    }, ba, 2)), [
      [N, e.show]
    ])
  ]);
}
const me = /* @__PURE__ */ D(ha, [["render", ya], ["__scopeId", "data-v-d6fb74b8"]]), va = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: me
}, Symbol.toStringTag, { value: "Module" })), wa = k({
  components: {
    Hamburger: me
    // Screenfull,
    // SizeSelect,
    // ThemePicker,
  },
  computed: {
    sidebar() {
      return this.$store.getters.sidebar;
    },
    userInfo() {
      return this.$store.getters.userInfo || {};
    },
    sysConfig() {
      return this.$store.getters.sysConfig;
    },
    urls() {
      return this.$store.getters.urls;
    },
    myAvatar() {
      const e = this;
      let t = e.userInfo && e.userInfo.avatar;
      return t ? (t.indexOf("http") !== 0 && (t = e.urls.baseUrl + t), t) : "https://wpimg.wallstcn.com/f778738c-e4f8-4870-b634-56703b4acafe.gif?imageView2/1/w/80/h/80";
    },
    displayName() {
      const e = this;
      return e.sysConfig && e.sysConfig.displayName ? e.sysConfig.displayName : "";
    }
  },
  methods: {
    toggleSideBar() {
      this.$store.dispatch("toggleSideBar");
    },
    logout() {
      const e = this;
      e.$api.user.logout().then(() => {
        e.$store.dispatch("logout"), location.reload();
      });
    }
  }
});
const Ia = { class: "navbar" }, Ta = { class: "left-menu" }, $a = { href: "/" }, Sa = { style: { display: "inline-block" } }, Ca = { class: "right-menu" }, Fa = /* @__PURE__ */ d("template", null, null, -1), Da = { class: "avatar-wrapper" }, ka = ["src"], Ua = { class: "user-info" }, Oa = /* @__PURE__ */ d("br", null, null, -1), Ea = /* @__PURE__ */ d("i", { class: "el-icon-caret-bottom" }, null, -1), Aa = /* @__PURE__ */ d("span", { style: { display: "inline-block" } }, "首页", -1), Pa = /* @__PURE__ */ d("span", { style: { display: "inline-block" } }, "个人信息", -1);
function Va(e, t, l, a, n, o) {
  const r = m("hamburger"), c = m("el-dropdown-item"), _ = m("router-link"), b = m("el-dropdown-menu"), $ = m("el-dropdown");
  return s(), h("div", Ia, [
    i(r, {
      "toggle-click": e.toggleSideBar,
      "is-active": e.sidebar.opened,
      class: "hamburger-container"
    }, null, 8, ["toggle-click", "is-active"]),
    d("div", Ta, [
      d("a", $a, [
        d("span", Sa, w(e.displayName), 1)
      ])
    ]),
    d("div", Ca, [
      Fa,
      i($, {
        class: "avatar-container right-menu-item",
        trigger: "click"
      }, {
        dropdown: u(() => [
          i(b, { class: "avatar-dropdown" }, {
            default: u(() => [
              i(_, { to: "/" }, {
                default: u(() => [
                  i(c, null, {
                    default: u(() => [
                      Aa
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }),
              i(_, { to: "/Admin/User/Info" }, {
                default: u(() => [
                  i(c, { divided: "" }, {
                    default: u(() => [
                      Pa
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }),
              i(c, { divided: "" }, {
                default: u(() => [
                  d("span", {
                    style: { display: "block" },
                    onClick: t[0] || (t[0] = (...y) => e.logout && e.logout(...y))
                  }, "退出登录")
                ]),
                _: 1
              })
            ]),
            _: 1
          })
        ]),
        default: u(() => [
          d("div", Da, [
            d("span", null, [
              d("img", {
                src: e.myAvatar,
                class: "user-avatar"
              }, null, 8, ka)
            ]),
            d("span", Ua, [
              I(w(e.userInfo && e.userInfo.displayName) + " ", 1),
              Oa,
              I(" [" + w(e.userInfo && e.userInfo.roleNames) + "] ", 1)
            ]),
            Ea
          ])
        ]),
        _: 1
      })
    ])
  ]);
}
const Na = /* @__PURE__ */ D(wa, [["render", Va]]), ja = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Na
}, Symbol.toStringTag, { value: "Module" })), La = k({
  name: "MenuItem",
  props: {
    icon: {
      type: String,
      default: ""
    },
    title: {
      type: String,
      default: ""
    }
  }
  // render(h, context) {
  //   console.log(context)
  //   const { icon, title } = context.props
  //   const vnodes = []
  //   if (icon) {
  //     if (icon.includes('el-icon')) {
  //       vnodes.push(<i class={[icon, 'sub-el-icon']} />)
  //     } else {
  //       vnodes.push(<svg-icon icon-class={icon} />)
  //     }
  //   }
  //   if (title) {
  //     vnodes.push(<span>{title}</span>)
  //   }
  //   return vnodes
  // }
});
const Ma = { key: 1 };
function Ra(e, t, l, a, n, o) {
  return s(), h("div", null, [
    e.icon ? (s(), h(S, { key: 0 }, [
      e.icon.includes("el-icon") ? (s(), h("i", {
        key: 0,
        class: P([e.icon, "sub-el-icon"])
      }, null, 2)) : T("", !0)
    ], 64)) : T("", !0),
    e.title ? (s(), h("span", Ma, w(e.title), 1)) : T("", !0)
  ]);
}
const Qe = /* @__PURE__ */ D(La, [["render", Ra], ["__scopeId", "data-v-81edd52c"]]), Ba = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Qe
}, Symbol.toStringTag, { value: "Module" })), za = k({
  name: "SidebarItem",
  components: { Item: Qe },
  props: {
    // route object
    item: {
      type: Object,
      required: !0
    },
    isNest: {
      type: Boolean,
      default: !1
    },
    basePath: {
      type: String,
      default: ""
    }
  },
  computed: {
    onlyOneChild() {
      const e = this, t = e.item.children, l = e.item;
      let a = [], n = null;
      return t && (a = t.filter((o) => o.hidden ? !1 : (n = o, !0))), a.length === 1 ? n : a.length === 0 ? (n = {
        ...l,
        /*: '',*/
        noShowingChildren: !0
      }, n) : null;
    }
  }
  // methods: {
  //   hasOneShowingChild(children: any[] = [], parent: any) {
  //     let showingChildren = []
  //     if (children) {
  //       showingChildren = children.filter((item) => {
  //         if (item.hidden) {
  //           return false
  //         } else {
  //           // Temp set(will be used if only has one showing child)
  //           this.onlyOneChild = item
  //           return true
  //         }
  //       })
  //     }
  //     // When there is only one child router, the child router is displayed by default
  //     if (showingChildren.length === 1) {
  //       return true
  //     }
  //     // Show parent if there are no child router to display
  //     if (showingChildren.length === 0) {
  //       this.onlyOneChild = { ...parent, /*: '',*/ noShowingChildren: true }
  //       return true
  //     }
  //     return false
  //   }
  // }
}), qa = { key: 0 };
function Ha(e, t, l, a, n, o) {
  const r = m("item"), c = m("el-menu-item"), _ = m("router-link"), b = m("sidebar-item", !0), $ = m("el-sub-menu");
  return e.item.visible ? (s(), h("div", qa, [
    e.onlyOneChild && (!e.onlyOneChild.children || e.onlyOneChild.noShowingChildren) && !e.item.alwaysShow ? (s(), h(S, { key: 0 }, [
      e.onlyOneChild ? (s(), v(_, {
        key: 0,
        to: e.onlyOneChild.path || e.onlyOneChild.url
      }, {
        default: u(() => [
          i(c, {
            index: e.onlyOneChild.path || e.onlyOneChild.url,
            class: P({ "submenu-title-noDropdown": !e.isNest })
          }, {
            default: u(() => [
              i(r, {
                title: e.onlyOneChild.displayName
              }, null, 8, ["title"])
            ]),
            _: 1
          }, 8, ["index", "class"])
        ]),
        _: 1
      }, 8, ["to"])) : T("", !0)
    ], 64)) : (s(), v($, {
      key: 1,
      ref: "subMenu",
      index: e.item.path || e.item.url,
      teleported: ""
    }, {
      title: u(() => [
        e.item ? (s(), v(r, {
          key: 0,
          icon: e.item.meta && e.item.meta.icon,
          title: e.item.displayName
        }, null, 8, ["icon", "title"])) : T("", !0)
      ]),
      default: u(() => [
        (s(!0), h(S, null, U(e.item.children, (y) => (s(), v(b, {
          key: y.path,
          "is-nest": !0,
          item: y,
          "base-path": y.path,
          class: "nest-menu"
        }, null, 8, ["item", "base-path"]))), 128))
      ]),
      _: 1
    }, 8, ["index"]))
  ])) : T("", !0);
}
const Xe = /* @__PURE__ */ D(za, [["render", Ha]]), xa = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Xe
}, Symbol.toStringTag, { value: "Module" }));
function Ka(e) {
  return localStorage.getItem(e);
}
function Wa(e, t) {
  localStorage.setItem(e, t);
}
function Ga(e) {
  localStorage.removeItem(e);
}
const E = {
  getItem: Ka,
  setItem: Wa,
  removeItem: Ga
}, pe = "menu";
function he() {
  const e = E.getItem(pe);
  return e ? JSON.parse(e) : null;
}
function te(e) {
  return E.setItem(pe, JSON.stringify(e));
}
function et() {
  return E.removeItem(pe);
}
const Ya = k({
  name: "Sidebar",
  components: { SidebarItem: Xe },
  computed: {
    menuRouters() {
      const e = this;
      let t = e.$store.getters.menuRouters;
      if (t && t.length > 0)
        return t;
      const l = he();
      if (l && l.length > 0) {
        const a = l;
        e.$store.dispatch("generateRoutes", a);
        const n = e.$store.getters.addRouters;
        n && n.forEach((o) => {
          e.$router.addRoute(o);
        });
      }
      return t = e.$store.getters.menuRouters, t;
    },
    sidebar() {
      return this.$store.getters.sidebar;
    },
    isCollapse() {
      return !this.sidebar.opened;
    }
  },
  data() {
    return {
      active: "1-1-1",
      data: []
    };
  },
  created() {
  }
});
const Ja = { class: "box" };
function Za(e, t, l, a, n, o) {
  const r = m("sidebar-item"), c = m("el-menu"), _ = m("el-scrollbar");
  return s(), h("div", Ja, [
    i(_, { "wrap-class": "scrollbar-wrapper" }, {
      default: u(() => [
        i(c, {
          "default-active": e.$route.path,
          collapse: e.isCollapse,
          "background-color": "#333333",
          "text-color": "#bfcbd9",
          "unique-opened": !0,
          "active-text-color": "#409EFF",
          "collapse-transition": !1,
          mode: "vertical"
        }, {
          default: u(() => [
            (s(!0), h(S, null, U(e.menuRouters, (b) => (s(), v(r, {
              key: b.path,
              item: b,
              "base-path": b.path
            }, null, 8, ["item", "base-path"]))), 128))
          ]),
          _: 1
        }, 8, ["default-active", "collapse"])
      ]),
      _: 1
    })
  ]);
}
const Qa = /* @__PURE__ */ D(Ya, [["render", Za], ["__scopeId", "data-v-3255e598"]]), Xa = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Qa
}, Symbol.toStringTag, { value: "Module" })), en = k({
  components: {
    Hamburger: me
    // Screenfull,
    // SizeSelect,
    // ThemePicker,
  },
  computed: {
    store() {
      return this.$store;
    },
    sidebar() {
      return this.$store.getters.sidebar;
    },
    userInfo() {
      return this.$store.getters.userInfo || {};
    },
    sysConfig() {
      return this.$store.getters.sysConfig;
    },
    urls() {
      return this.$store.getters.urls;
    },
    myAvatar() {
      const e = this;
      let t = e.userInfo && e.userInfo.avatar;
      return t ? (t.indexOf("http") !== 0 && (t = e.urls.baseUrl + t), t) : "https://wpimg.wallstcn.com/f778738c-e4f8-4870-b634-56703b4acafe.gif?imageView2/1/w/80/h/80";
    },
    displayName() {
      const e = this;
      return e.sysConfig && e.sysConfig.displayName ? e.sysConfig.displayName : "";
    }
  },
  methods: {
    toggleSideBar() {
      this.store.dispatch("toggleSideBar");
    },
    logout() {
      const e = this;
      e.$api.user.logout().then(() => {
        e.store.dispatch("logout"), location.reload();
      });
    }
  }
});
const tn = { class: "navbar" }, ln = { class: "left-menu" }, an = { href: "/" }, nn = { style: { display: "inline-block" } }, on = { class: "right-menu" }, rn = /* @__PURE__ */ d("template", null, null, -1), sn = { class: "avatar-wrapper" }, un = ["src"], dn = { class: "user-info" }, cn = /* @__PURE__ */ d("br", null, null, -1), mn = /* @__PURE__ */ d("i", { class: "el-icon-caret-bottom" }, null, -1), pn = /* @__PURE__ */ d("span", { style: { display: "inline-block" } }, "首页", -1), hn = /* @__PURE__ */ d("span", { style: { display: "inline-block" } }, "个人信息", -1);
function fn(e, t, l, a, n, o) {
  const r = m("hamburger"), c = m("el-dropdown-item"), _ = m("router-link"), b = m("el-dropdown-menu"), $ = m("el-dropdown");
  return s(), h("div", tn, [
    i(r, {
      "toggle-click": e.toggleSideBar,
      "is-active": e.sidebar.opened,
      class: "hamburger-container"
    }, null, 8, ["toggle-click", "is-active"]),
    d("div", ln, [
      d("a", an, [
        d("span", nn, w(e.displayName), 1)
      ])
    ]),
    d("div", on, [
      rn,
      i($, {
        class: "avatar-container right-menu-item",
        trigger: "click"
      }, {
        dropdown: u(() => [
          i(b, { class: "avatar-dropdown" }, {
            default: u(() => [
              i(_, { to: "/" }, {
                default: u(() => [
                  i(c, null, {
                    default: u(() => [
                      pn
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }),
              i(_, { to: "/Admin/User/Info" }, {
                default: u(() => [
                  i(c, { divided: "" }, {
                    default: u(() => [
                      hn
                    ]),
                    _: 1
                  })
                ]),
                _: 1
              }),
              i(c, { divided: "" }, {
                default: u(() => [
                  d("span", {
                    style: { display: "block" },
                    onClick: t[0] || (t[0] = (...y) => e.logout && e.logout(...y))
                  }, "退出登录")
                ]),
                _: 1
              })
            ]),
            _: 1
          })
        ]),
        default: u(() => [
          d("div", sn, [
            d("span", null, [
              d("img", {
                src: e.myAvatar,
                class: "user-avatar"
              }, null, 8, un)
            ]),
            d("span", dn, [
              I(w(e.userInfo && e.userInfo.displayName) + " ", 1),
              cn,
              I(" [" + w(e.userInfo && e.userInfo.roleNames) + "] ", 1)
            ]),
            mn
          ])
        ]),
        _: 1
      })
    ])
  ]);
}
const le = /* @__PURE__ */ D(en, [["render", fn]]), gn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: le
}, Symbol.toStringTag, { value: "Module" })), _n = k({
  name: "MenuItem",
  props: {
    icon: {
      type: String,
      default: ""
    },
    title: {
      type: String,
      default: ""
    }
  }
  // render(h, context) {
  //   console.log(context)
  //   const { icon, title } = context.props
  //   const vnodes = []
  //   if (icon) {
  //     if (icon.includes('el-icon')) {
  //       vnodes.push(<i class={[icon, 'sub-el-icon']} />)
  //     } else {
  //       vnodes.push(<svg-icon icon-class={icon} />)
  //     }
  //   }
  //   if (title) {
  //     vnodes.push(<span>{title}</span>)
  //   }
  //   return vnodes
  // }
});
const bn = { key: 1 };
function yn(e, t, l, a, n, o) {
  return s(), h("div", null, [
    e.icon ? (s(), h(S, { key: 0 }, [
      e.icon.includes("el-icon") ? (s(), h("i", {
        key: 0,
        class: P([e.icon, "sub-el-icon"])
      }, null, 2)) : T("", !0)
    ], 64)) : T("", !0),
    e.title ? (s(), h("span", bn, w(e.title), 1)) : T("", !0)
  ]);
}
const tt = /* @__PURE__ */ D(_n, [["render", yn], ["__scopeId", "data-v-92147a3c"]]), vn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: tt
}, Symbol.toStringTag, { value: "Module" })), wn = k({
  name: "SidebarItem",
  components: { Item: tt },
  props: {
    // route object
    item: {
      type: Object,
      required: !0
    },
    isNest: {
      type: Boolean,
      default: !1
    },
    basePath: {
      type: String,
      default: ""
    }
  },
  computed: {
    onlyOneChild() {
      const e = this, t = e.item.children, l = e.item;
      let a = [], n = null;
      return t && (a = t.filter((o) => o.hidden ? !1 : (n = o, !0))), a.length === 1 ? n : a.length === 0 ? (n = {
        ...l,
        /*: '',*/
        noShowingChildren: !0
      }, n) : null;
    }
  }
  // methods: {
  //   hasOneShowingChild(children: any[] = [], parent: any) {
  //     let showingChildren = []
  //     if (children) {
  //       showingChildren = children.filter((item) => {
  //         if (item.hidden) {
  //           return false
  //         } else {
  //           // Temp set(will be used if only has one showing child)
  //           this.onlyOneChild = item
  //           return true
  //         }
  //       })
  //     }
  //     // When there is only one child router, the child router is displayed by default
  //     if (showingChildren.length === 1) {
  //       return true
  //     }
  //     // Show parent if there are no child router to display
  //     if (showingChildren.length === 0) {
  //       this.onlyOneChild = { ...parent, /*: '',*/ noShowingChildren: true }
  //       return true
  //     }
  //     return false
  //   }
  // }
}), In = { key: 0 };
function Tn(e, t, l, a, n, o) {
  const r = m("item"), c = m("el-menu-item"), _ = m("router-link"), b = m("sidebar-item", !0), $ = m("el-sub-menu");
  return e.item.visible ? (s(), h("div", In, [
    e.onlyOneChild && (!e.onlyOneChild.children || e.onlyOneChild.noShowingChildren) && !e.item.alwaysShow ? (s(), h(S, { key: 0 }, [
      e.onlyOneChild ? (s(), v(_, {
        key: 0,
        to: e.onlyOneChild.path || e.onlyOneChild.url
      }, {
        default: u(() => [
          i(c, {
            index: e.onlyOneChild.path || e.onlyOneChild.url,
            class: P({ "submenu-title-noDropdown": !e.isNest })
          }, {
            default: u(() => [
              i(r, {
                title: e.onlyOneChild.displayName
              }, null, 8, ["title"])
            ]),
            _: 1
          }, 8, ["index", "class"])
        ]),
        _: 1
      }, 8, ["to"])) : T("", !0)
    ], 64)) : (s(), v($, {
      key: 1,
      ref: "subMenu",
      index: e.item.path || e.item.url,
      teleported: ""
    }, {
      title: u(() => [
        e.item ? (s(), v(r, {
          key: 0,
          icon: e.item.meta && e.item.meta.icon,
          title: e.item.displayName
        }, null, 8, ["icon", "title"])) : T("", !0)
      ]),
      default: u(() => [
        (s(!0), h(S, null, U(e.item.children, (y) => (s(), v(b, {
          key: y.path,
          "is-nest": !0,
          item: y,
          "base-path": y.path,
          class: "nest-menu"
        }, null, 8, ["item", "base-path"]))), 128))
      ]),
      _: 1
    }, 8, ["index"]))
  ])) : T("", !0);
}
const lt = /* @__PURE__ */ D(wn, [["render", Tn]]), $n = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: lt
}, Symbol.toStringTag, { value: "Module" })), Sn = k({
  name: "Sidebar",
  components: { SidebarItem: lt },
  computed: {
    menuRouters() {
      const e = this;
      let t = e.$store.getters.menuRouters;
      if (t && t.length > 0)
        return t;
      const l = he();
      if (l && l.length > 0) {
        const a = l;
        e.$store.dispatch("generateRoutes", a);
        const n = e.$store.getters.addRouters;
        n && n.forEach((o) => {
          e.$router.addRoute(o);
        });
      }
      return t = e.$store.getters.menuRouters, t;
    },
    sidebar() {
      return this.$store.getters.sidebar;
    },
    isCollapse() {
      return !this.sidebar.opened;
    }
  },
  data() {
    return {
      active: "1-1-1",
      data: []
    };
  },
  created() {
  }
});
const Cn = { class: "box" };
function Fn(e, t, l, a, n, o) {
  const r = m("sidebar-item"), c = m("el-menu"), _ = m("el-scrollbar");
  return s(), h("div", Cn, [
    i(_, { "wrap-class": "scrollbar-wrapper" }, {
      default: u(() => [
        i(c, {
          "default-active": e.$route.path,
          collapse: e.isCollapse,
          "background-color": "#333333",
          "text-color": "#bfcbd9",
          "unique-opened": !0,
          "active-text-color": "#409EFF",
          "collapse-transition": !1,
          mode: "vertical"
        }, {
          default: u(() => [
            (s(!0), h(S, null, U(e.menuRouters, (b) => (s(), v(r, {
              key: b.path,
              item: b,
              "base-path": b.path
            }, null, 8, ["item", "base-path"]))), 128))
          ]),
          _: 1
        }, 8, ["default-active", "collapse"])
      ]),
      _: 1
    })
  ]);
}
const ae = /* @__PURE__ */ D(Sn, [["render", Fn], ["__scopeId", "data-v-a43a4bae"]]), Dn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ae
}, Symbol.toStringTag, { value: "Module" })), kn = k({
  name: "AppMain",
  computed: {
    // cachedViews() {
    //   return this.$store.state.tagsView.cachedViews
    // },
    key() {
      return this.$route.fullPath;
    }
  }
});
const Un = { class: "app-main" };
function On(e, t, l, a, n, o) {
  const r = m("router-view");
  return s(), h("section", Un, [
    (s(), v(r, { key: e.key }))
  ]);
}
const ne = /* @__PURE__ */ D(kn, [["render", On], ["__scopeId", "data-v-62c70034"]]), En = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ne
}, Symbol.toStringTag, { value: "Module" })), { body: An } = document, Pn = 1024, Vn = 3, at = {
  data() {
    return {
      currentDevice: "desktop"
    };
  },
  watch: {
    $route() {
      this.device === "mobile" && this.sidebar.opened && (this.currentDevice = "mobile", this.$store.dispatch("closeSideBar", { withoutAnimation: !1 }));
    }
  },
  beforeMount() {
    window.addEventListener("resize", this.resizeHandler);
  },
  mounted() {
    this.isMobile() && (this.currentDevice = "mobile", this.$store.dispatch("toggleDevice", "mobile"), this.$store.dispatch("closeSideBar", { withoutAnimation: !0 }));
  },
  methods: {
    isMobile() {
      return An.getBoundingClientRect().width - Vn < Pn;
    },
    resizeHandler() {
      if (!document.hidden) {
        const e = this.isMobile();
        e && this.currentDevice !== "mobile" ? (this.currentDevice = "mobile", this.$store.dispatch("toggleDevice", "mobile"), this.$store.dispatch("closeSideBar", { withoutAnimation: !0 })) : !e && this.currentDevice !== "desktop" && (this.currentDevice = "desktop", this.$store.dispatch("toggleDevice", "desktop"), this.$store.dispatch("toggleSideBar"));
      }
    }
  }
};
const Nn = {
  components: {
    Navbar: le,
    Sidebar: ae,
    AppMain: ne
  },
  mixins: [at],
  computed: {
    sidebar() {
      return this.$store.state.app.sidebar;
    },
    device() {
      return this.$store.state.app.device;
    },
    hiddenLayout() {
      let e = this.$route.query;
      return e.hiddenLayout === "true" || e.hl === "true";
    },
    classObj() {
      return {
        hideSidebar: !this.sidebar.opened,
        openSidebar: this.sidebar.opened,
        withoutAnimation: this.sidebar.withoutAnimation,
        mobile: this.device === "mobile"
      };
    },
    classAppMain() {
      return {
        hiddenLayout: this.hiddenLayout,
        hideSidebarMain: !this.sidebar.opened,
        openSidebarMain: this.sidebar.opened
      };
    }
  },
  methods: {
    handleClickOutside() {
      this.$store.dispatch("closeSideBar", { withoutAnimation: !1 });
    }
  }
};
function jn(e, t, l, a, n, o) {
  const r = m("navbar"), c = m("sidebar"), _ = m("app-main");
  return s(), h("div", {
    class: P([o.classObj, "app-wrapper"])
  }, [
    d("div", null, [
      A(d("div", {
        class: "drawer-bg",
        onClick: t[0] || (t[0] = (...b) => o.handleClickOutside && o.handleClickOutside(...b))
      }, null, 512), [
        [N, !o.hiddenLayout && o.device === "mobile" && o.sidebar.opened]
      ]),
      A(i(r, null, null, 512), [
        [N, !o.hiddenLayout]
      ]),
      A(i(c, { class: "sidebar sidebar-container" }, null, 512), [
        [N, !o.hiddenLayout]
      ]),
      d("div", {
        class: P([o.classAppMain, "main"])
      }, [
        i(_)
      ], 2)
    ])
  ], 2);
}
const Ln = /* @__PURE__ */ D(Nn, [["render", jn], ["__scopeId", "data-v-2db399f4"]]), Mn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Ln
}, Symbol.toStringTag, { value: "Module" })), Rn = k({
  name: "AuthRedirect",
  async created() {
    let e = this;
    const t = e.$route.query.redirect || "/";
    let l = e.$route.hash.replace("#token=", "");
    e.$store.dispatch("setToken", l), e.$api.user.getUserInfo().then((a) => {
      const n = a.data;
      e.$store.dispatch("setUserInfo", n);
    }), e.$api.menu.getMenu().then((a) => {
      const n = a.data;
      te(n), e.$store.dispatch("generateRoutes", n);
      const o = e.$store.getters.addRouters;
      o && o.forEach((r) => {
        e.$router.addRoute(r);
      }), e.$router.push({ path: t });
    });
  },
  render() {
    return "";
  }
}), Bn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Rn
}, Symbol.toStringTag, { value: "Module" })), zn = {}, qn = /* @__PURE__ */ d("p", null, "This is umi docs.", -1), Hn = [
  qn
];
function xn(e, t) {
  return s(), h("div", null, Hn);
}
const Kn = /* @__PURE__ */ D(zn, [["render", xn]]), Wn = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Kn
}, Symbol.toStringTag, { value: "Module" })), Gn = {}, Yn = /* @__PURE__ */ d("h2", null, "Yay! Welcome to umi ❤️ vue!", -1), Jn = /* @__PURE__ */ d("p", null, null, -1), Zn = /* @__PURE__ */ d("p", null, [
  /* @__PURE__ */ I("To get started, edit "),
  /* @__PURE__ */ d("code", null, "pages/index.vue"),
  /* @__PURE__ */ I(" and save to reload.")
], -1), Qn = [
  Yn,
  Jn,
  Zn
];
function Xn(e, t) {
  return s(), h("div", null, Qn);
}
const eo = /* @__PURE__ */ D(Gn, [["render", Xn]]), to = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: eo
}, Symbol.toStringTag, { value: "Module" })), lo = k({
  data() {
    return {
      loginForm: {
        username: null,
        password: null,
        remember: !0
      }
    };
  },
  computed: {
    sysConfig() {
      return this.$store.getters.sysConfig;
    },
    loginConfig() {
      let t = this.$store.getters.loginConfig;
      return t || (t = {
        displayName: "魔方",
        logo: "",
        // 系统logo
        allowLogin: !0,
        allowRegister: !0,
        providers: []
      }), t;
    },
    baseUrl() {
      return this.$store.getters.urls.baseUrl;
    },
    redirect() {
      return this.$route.query.redirect;
    },
    displayName() {
      let e = this;
      return e.sysConfig && e.sysConfig.displayName || e.loginConfig && e.loginConfig.displayName;
    }
  },
  created() {
    let e = this;
    try {
      e.$messageBox.close();
    } catch {
    }
    e.autoAuthRedirect(), e.$api.config.getLoginConfig().then((t) => {
      let l = t.data;
      e.$store.dispatch("setLoginConfig", l);
    });
  },
  methods: {
    login() {
      let e = this;
      e.$api.user.login(e.loginForm).then(async (t) => {
        let a = t.data.token;
        await e.$store.dispatch("setToken", a), e.$api.user.getUserInfo().then((n) => {
          const o = n.data;
          e.$store.dispatch("setUserInfo", o);
        }), e.$api.menu.getMenu().then((n) => {
          const o = n.data;
          te(o), e.$store.dispatch("generateRoutes", o);
          const r = e.$store.getters.addRouters;
          r && r.forEach((c) => {
            e.$router.addRoute(c);
          }), e.$router.push({ path: e.redirect || "/" });
        }), e.$api.config.getObject("/Admin/Sys").then((n) => {
          const o = n.data.value;
          e.$store.dispatch("setSysConfig", o);
        });
      });
    },
    ssoClick(e) {
      location.href = this.baseUrl + e;
    },
    getUrl(e) {
      let t = this, l = `/Sso/Login?name=${e.name}&source=front-end`, a = encodeURIComponent(
        location.origin + "/auth-redirect" + (t.redirect ? "?redirect=" + t.redirect : "")
      );
      return l += `&redirect_uri=${a}`, l;
    },
    getLogoUrl(e) {
      let t = this;
      return e.indexOf("http") !== 0 && (e = t.baseUrl + e), e;
    },
    autoAuthRedirect() {
      let e = this, t = e.loginConfig;
      t && !t.allowLogin && t.providers.length === 1 && e.ssoClick(e.getUrl(t.providers[0]));
    }
  }
});
const nt = (e) => (B("data-v-a60c4660"), e = e(), z(), e), ao = { class: "center" }, no = { class: "login-col" }, oo = /* @__PURE__ */ nt(() => /* @__PURE__ */ d("i", { class: "el-icon-cloudy" }, null, -1)), ro = { class: "heading text-primary" }, so = {
  key: 0,
  class: "center"
}, io = /* @__PURE__ */ nt(() => /* @__PURE__ */ d("p", { class: "login3" }, [
  /* @__PURE__ */ d("span", { class: "left" }),
  /* @__PURE__ */ I(" 第三方登录 "),
  /* @__PURE__ */ d("span", { class: "right" })
], -1)), uo = ["title", "onClick"], co = ["src"];
function mo(e, t, l, a, n, o) {
  const r = m("el-col"), c = m("el-row"), _ = m("el-input"), b = m("el-form-item"), $ = m("el-checkbox"), y = m("el-form");
  return s(), h("div", ao, [
    d("div", no, [
      d("div", null, [
        i(c, null, {
          default: u(() => [
            i(r, {
              span: 24,
              class: "login-logo"
            }, {
              default: u(() => [
                oo
              ]),
              _: 1
            })
          ]),
          _: 1
        }),
        e.loginConfig.allowLogin ? (s(), h(S, { key: 0 }, [
          i(y, {
            model: e.loginForm,
            size: "default",
            class: "cube-login"
          }, {
            default: u(() => [
              d("span", ro, w(e.displayName) + " 登录", 1),
              i(b, { label: "" }, {
                default: u(() => [
                  i(_, {
                    modelValue: e.loginForm.username,
                    "onUpdate:modelValue": t[0] || (t[0] = (p) => e.loginForm.username = p),
                    placeholder: "用户名 / 邮箱",
                    "prefix-icon": "el-icon-user"
                  }, null, 8, ["modelValue"])
                ]),
                _: 1
              }),
              i(b, { label: "" }, {
                default: u(() => [
                  i(_, {
                    modelValue: e.loginForm.password,
                    "onUpdate:modelValue": t[1] || (t[1] = (p) => e.loginForm.password = p),
                    placeholder: "密码",
                    "prefix-icon": "el-icon-lock",
                    "show-password": ""
                  }, null, 8, ["modelValue"])
                ]),
                _: 1
              }),
              i(b, { label: "" }, {
                default: u(() => [
                  i($, {
                    class: "text text-primary",
                    modelValue: e.loginForm.remember,
                    "onUpdate:modelValue": t[2] || (t[2] = (p) => e.loginForm.remember = p)
                  }, {
                    default: u(() => [
                      I(" 记住我 ")
                    ]),
                    _: 1
                  }, 8, ["modelValue"])
                ]),
                _: 1
              })
            ]),
            _: 1
          }, 8, ["model"]),
          d("button", {
            class: "btn",
            onClick: t[3] || (t[3] = (...p) => e.login && e.login(...p))
          }, "登录")
        ], 64)) : T("", !0)
      ]),
      e.loginConfig.providers.length > 0 ? (s(), h("div", so, [
        io,
        i(c, null, {
          default: u(() => [
            i(r, { sm: 24 }, {
              default: u(() => [
                (s(!0), h(S, null, U(e.loginConfig.providers, (p, f) => (s(), h("a", {
                  key: f,
                  title: p.nickName || p.name,
                  onClick: (g) => e.ssoClick(e.getUrl(p))
                }, [
                  p.logo ? (s(), h("img", {
                    key: 0,
                    src: e.getLogoUrl(p.logo),
                    style: { width: "64px", height: "64px" }
                  }, null, 8, co)) : (s(), h(S, { key: 1 }, [
                    I(w(p.nickName || p.name), 1)
                  ], 64))
                ], 8, uo))), 128))
              ]),
              _: 1
            })
          ]),
          _: 1
        })
      ])) : T("", !0)
    ])
  ]);
}
const po = /* @__PURE__ */ D(lo, [["render", mo], ["__scopeId", "data-v-a60c4660"]]), ho = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: po
}, Symbol.toStringTag, { value: "Module" })), fo = {}, go = /* @__PURE__ */ d("p", null, "Test.", -1), _o = [
  go
];
function bo(e, t) {
  return s(), h("div", null, _o);
}
const yo = /* @__PURE__ */ D(fo, [["render", bo]]), vo = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: yo
}, Symbol.toStringTag, { value: "Module" })), wo = k({
  props: ["path"],
  data() {
    return {
      form: {},
      properties: []
    };
  },
  computed: {
    currentPath() {
      return this.path;
    }
  },
  watch: {
    $route: {
      handler: function() {
        this.init();
      },
      immediate: !0
    }
  },
  methods: {
    init() {
      this.query();
    },
    query() {
      let e = this;
      e.$api.config.getObject(e.currentPath).then((t) => {
        e.form = t.data.value, e.properties = t.data.properties;
      });
    },
    confirm() {
      let e = this;
      e.$api.config.updateObject(e.currentPath, e.form).then(() => {
        let t = "保存成功";
        e.form.enableNewUI || (t += "，正在跳转页面"), e.$message({
          message: t,
          type: "success",
          duration: 3 * 1e3
        }), e.form.enableNewUI || (location.href = "/");
      });
    }
  }
});
const Io = { class: "objform" }, To = { style: { position: "fixed", margin: "30px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } };
function $o(e, t, l, a, n, o) {
  const r = m("el-switch"), c = m("el-date-picker"), _ = m("el-input"), b = m("el-form-item"), $ = m("el-button"), y = m("el-form");
  return s(), h("div", Io, [
    i(y, {
      "label-position": "right",
      "label-width": "120px",
      ref: "form",
      model: e.form
    }, {
      default: u(() => [
        (s(!0), h(S, null, U(e.properties, (p, f) => (s(), h(S, null, [
          p.length > 0 ? (s(), h("div", { key: f }, [
            (s(), h("div", { key: f }, [
              d("label", null, [
                d("h2", null, w(f), 1)
              ])
            ])),
            (s(!0), h(S, null, U(p, (g, C) => (s(), v(b, {
              key: C + f,
              label: g.displayName,
              prop: g.name
            }, {
              default: u(() => [
                g.typeStr == "Boolean" ? (s(), v(r, {
                  key: 0,
                  modelValue: e.form[g.name],
                  "onUpdate:modelValue": (F) => e.form[g.name] = F,
                  "active-color": "#13ce66",
                  "inactive-color": "#ff4949"
                }, null, 8, ["modelValue", "onUpdate:modelValue"])) : g.typeStr == "DateTime" ? (s(), v(c, {
                  key: 1,
                  modelValue: e.form[g.name],
                  "onUpdate:modelValue": (F) => e.form[g.name] = F,
                  type: "datetime",
                  format: "YYYY-MM-DD HH:mm:ss",
                  "value-format": "YYYY-MM-DD HH:mm:ss"
                }, null, 8, ["modelValue", "onUpdate:modelValue"])) : (s(), v(_, {
                  key: 2,
                  modelValue: e.form[g.name],
                  "onUpdate:modelValue": (F) => e.form[g.name] = F,
                  type: "text",
                  size: "default"
                }, null, 8, ["modelValue", "onUpdate:modelValue"])),
                d("span", null, w(g.description), 1)
              ]),
              _: 2
            }, 1032, ["label", "prop"]))), 128))
          ])) : T("", !0)
        ], 64))), 256)),
        i(b, {
          prop: "",
          "label-name": ""
        }, {
          default: u(() => [
            d("div", To, [
              i($, {
                type: "primary",
                onClick: e.confirm
              }, {
                default: u(() => [
                  I("保存")
                ]),
                _: 1
              }, 8, ["onClick"])
            ])
          ]),
          _: 1
        })
      ]),
      _: 1
    }, 8, ["model"])
  ]);
}
const ot = /* @__PURE__ */ D(wo, [["render", $o], ["__scopeId", "data-v-3ea25781"]]), So = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ot
}, Symbol.toStringTag, { value: "Module" })), q = {
  none: 0,
  detail: 1,
  insert: 2,
  update: 4,
  delete: 8
}, Co = k({
  name: "List",
  components: {
    AdvancedTable: Ze
  },
  props: {
    // 搜索字段配置
    tableSearchConfig: {
      type: Array,
      default: () => [
        {
          itemType: "datePicker",
          name: "dtStart$dtEnd",
          displayName: "时间范围",
          showInSearch: !0,
          options: { type: "daterange", setDefaultValue: !1 }
        },
        {
          name: "Q",
          displayName: "",
          showInSearch: !0,
          options: {
            placeholder: "请输入关键字"
          }
        }
      ]
    },
    // 表格操作按钮配置
    tableHandlerConfig: {
      type: Array,
      default: () => []
    },
    // 列表字段配置
    tableColumnConfig: {
      type: Array,
      default: () => []
    },
    // 列表操作按钮配置
    tableActionConfig: {
      type: Array,
      default: () => []
    }
  },
  data() {
    return {
      tableData: [],
      actionList: [
        {
          name: "handler",
          displayName: "操作",
          width: "155px",
          showInList: !0,
          handlerList: [
            {
              innerText: "查看",
              handler: "detail",
              if: () => !this.hasPermission(q.update) && this.hasPermission(q.detail)
            },
            {
              innerText: "编辑",
              handler: "editData",
              if: () => this.hasPermission(q.update)
            },
            {
              innerText: "删除",
              type: "danger",
              handler: "deleteData",
              if: () => this.hasPermission(q.delete)
            }
          ]
        }
      ],
      headerData: []
    };
  },
  computed: {
    tableHandlerList() {
      const e = this;
      return e.tableHandlerConfig.length < 1 ? [
        {
          name: "新增",
          handler: "add",
          type: "primary",
          if: () => this.hasPermission(q.insert)
        }
      ] : e.tableHandlerConfig.map((t) => {
        const l = t.if;
        return typeof l == "function" && (t.if = (a) => l(e, a)), t;
      });
    },
    columns() {
      const e = this;
      let t = e.tableColumnConfig, l = e.tableActionConfig;
      t.length === 0 && (t = e.headerData), l.length === 0 && (l = e.actionList);
      const a = e.tableSearchConfig.concat(
        t.concat(l)
      );
      for (const n of a) {
        const o = n.if;
        typeof o == "function" && (n.if = (r) => o(e, r)), n.handlerList && n.handlerList.length > 0 && (n.handlerList = n.handlerList.map((r) => {
          const c = r.if;
          return typeof c == "function" && (r.if = (_) => c(e, _)), r;
        }));
      }
      return a;
    },
    currentPath() {
      return this.$route.path;
    },
    advancedTable() {
      return this.$refs.advancedTable;
    },
    // 批量选中的数据
    batchList() {
      return this.advancedTable.selectList;
    }
  },
  created() {
    this.init();
  },
  activated() {
    this.init();
  },
  methods: {
    init() {
      this.getColumns();
    },
    // 获取表格数据
    getDataList() {
      this.advancedTable.getDataList();
    },
    getColumns() {
      const e = this;
      if (e.tableColumnConfig.length > 0)
        return;
      const t = e.currentPath;
      e.$api.base.getColumns(t).then((l) => {
        e.headerData = l.data;
      });
    },
    add() {
      const e = this;
      e.$router.push(e.currentPath + "/Add");
    },
    detail(e) {
      const t = this;
      t.$router.push(t.currentPath + "/Detail/" + e.row.id);
    },
    editData(e) {
      const t = this;
      t.$router.push(t.currentPath + "/Edit/" + e.row.id);
    },
    deleteData(e) {
      const t = this;
      t.$api.base.deleteById(t.currentPath, e.row.id).then(() => {
        t.getDataList();
      });
    },
    rowDblclick(e) {
      this.editData({ row: e });
    },
    // 判断操作id会否有权限
    hasPermission(e) {
      const t = this, l = t.$route.meta.menuId, a = t.$route.meta.permissions;
      return t.$store.state.user.hasPermission(t.$store, {
        menuId: l,
        actionId: e,
        permissions: a
      });
    }
  }
});
const Fo = { class: "list-container" };
function Do(e, t, l, a, n, o) {
  const r = m("AdvancedTable");
  return s(), h("div", Fo, [
    i(r, {
      ref: "advancedTable",
      searchList: e.columns,
      tableHandlerList: e.tableHandlerList,
      columns: e.columns,
      url: e.currentPath
    }, null, 8, ["searchList", "tableHandlerList", "columns", "url"])
  ]);
}
const rt = /* @__PURE__ */ D(Co, [["render", Do], ["__scopeId", "data-v-46aa907a"]]), ko = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: rt
}, Symbol.toStringTag, { value: "Module" })), Uo = {
  components: {
    ObjectForm: ot
  },
  data() {
    return {
      activeName: "Cube"
    };
  },
  methods: {
    handleClick(e, t) {
      let l = this;
      e.name === "OAuthConfig" && l.$router.push({ path: "/Admin/OAuthConfig", component: rt });
    }
  }
};
function Oo(e, t, l, a, n, o) {
  const r = m("object-form"), c = m("el-tab-pane"), _ = m("router-link"), b = m("el-tabs");
  return s(), v(b, {
    modelValue: n.activeName,
    "onUpdate:modelValue": t[0] || (t[0] = ($) => n.activeName = $),
    onTabClick: o.handleClick
  }, {
    default: u(() => [
      i(c, {
        label: "魔方设置",
        name: "Cube"
      }, {
        default: u(() => [
          i(r, { path: "/Admin/Cube" })
        ]),
        _: 1
      }),
      i(c, {
        label: "基础设置",
        name: "Core"
      }, {
        default: u(() => [
          i(r, { path: "/Admin/Core" })
        ]),
        _: 1
      }),
      i(c, {
        label: "系统设置",
        name: "Sys"
      }, {
        default: u(() => [
          i(r, { path: "/Admin/Sys" })
        ]),
        _: 1
      }),
      i(c, {
        label: "数据中间件",
        name: "XCode"
      }, {
        default: u(() => [
          i(r, { path: "/Admin/XCode" })
        ]),
        _: 1
      }),
      i(c, {
        label: "OAuth设置",
        name: "OAuthConfig"
      }, {
        default: u(() => [
          i(_, { to: "/Admin/OAuthConfig" }, {
            default: u(() => [
              I("跳转OAuth设置")
            ]),
            _: 1
          })
        ]),
        _: 1
      })
    ]),
    _: 1
  }, 8, ["modelValue", "onTabClick"]);
}
const Eo = /* @__PURE__ */ D(Uo, [["render", Oo]]), Ao = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Eo
}, Symbol.toStringTag, { value: "Module" })), Po = {
  data() {
    return {
      model: {
        rawUrl: "RawUrl",
        contentRootPath: "ContentRootPath",
        host: "Host",
        localHost: "LocalHost",
        remoteHost: "RemoteHost",
        commandLine: "CommandLine",
        processName: "ProcessName",
        curDomainFriendlyName: "FriendlyName",
        envVersion: "EnvVersion",
        frameworkName: "FrameworkName",
        guid: "Guid",
        oSName: "OSName",
        oSVersion: "OSVersion",
        product: "Product",
        userName: "UserName",
        machineName: "MachineName",
        uuid: "UUID",
        processor: "Processor",
        processorCount: "ProcessorCount",
        cpuRate: "CpuRate",
        temperature: 0,
        dateTimeNow: "DateTimeNow",
        uptime: "Uptime",
        availableMemory: "AvailableMemory",
        memory: "Memory",
        workingSet64: "WorkingSet64",
        privateMemorySize64: "PrivateMemorySize64",
        totalMemory: "TotalMemory"
      }
    };
  },
  methods: {
    restart() {
      this.$alert(
        "仅重启ASP.Net Core应用程序域，而不是操作系统！<br/>确认重启？",
        "提示",
        {
          confirmButtonText: "confirm",
          callback: (e) => {
            console.log(e);
          }
        }
      );
    },
    memoryFree() {
      console.log("memoryFree");
    },
    main(e) {
      console.log(e);
    }
  }
}, Vo = { class: "table-responsive" }, No = { class: "table table-bordered table-hover table-striped table-condensed" }, jo = /* @__PURE__ */ d("thead", null, [
  /* @__PURE__ */ d("tr", null, [
    /* @__PURE__ */ d("th", { colspan: "4" }, " 服务器信息 ")
  ])
], -1), Lo = /* @__PURE__ */ d("td", { class: "name" }, " 应用系统： ", -1), Mo = { class: "value" }, Ro = /* @__PURE__ */ d("td", { class: "name" }, " 目录： ", -1), Bo = { class: "value" }, zo = /* @__PURE__ */ d("td", { class: "name" }, " 域名地址： ", -1), qo = { class: "value" }, Ho = { title: "主机" }, xo = { title: "本地" }, Ko = { title: "远程" }, Wo = /* @__PURE__ */ d("td", { class: "name" }, " 应用程序： ", -1), Go = { class: "value" }, Yo = ["title"], Jo = /* @__PURE__ */ d("td", { class: "name" }, " 应用域： ", -1), Zo = { class: "value" }, Qo = /* @__PURE__ */ d("td", { class: "name" }, " .net 版本： ", -1), Xo = { class: "value" }, er = /* @__PURE__ */ d("td", { class: "name" }, " 操作系统： ", -1), tr = ["title"], lr = /* @__PURE__ */ d("td", { class: "name" }, " 机器用户： ", -1), ar = ["title"], nr = { key: 0 }, or = /* @__PURE__ */ d("td", { class: "name" }, " 处理器： ", -1), rr = ["title"], sr = { key: 0 }, ir = /* @__PURE__ */ d("td", { class: "name" }, " 时间： ", -1), ur = {
  class: "value",
  title: "这里使用了服务器默认的时间格式！后面是开机时间。"
}, dr = /* @__PURE__ */ d("td", { class: "name" }, " 内存： ", -1), cr = { class: "value" }, mr = /* @__PURE__ */ d("td", { class: "name" }, " 进程时间： ", -1), pr = /* @__PURE__ */ d("td", { class: "value" }, null, -1), hr = /* @__PURE__ */ d("td", { class: "name" }, " Session： ", -1), fr = { class: "value" }, gr = /* @__PURE__ */ d("td", { class: "name" }, " 应用启动： ", -1), _r = /* @__PURE__ */ d("td", { class: "value" }, null, -1), br = /* @__PURE__ */ d("table", { class: "table table-bordered table-hover table-striped table-condensed" }, [
  /* @__PURE__ */ d("thead", null, [
    /* @__PURE__ */ d("tr", null, [
      /* @__PURE__ */ d("th", null, "名称"),
      /* @__PURE__ */ d("th", null, "标题"),
      /* @__PURE__ */ d("th", null, "文件版本"),
      /* @__PURE__ */ d("th", null, "内部版本"),
      /* @__PURE__ */ d("th", null, "编译时间"),
      /* @__PURE__ */ d("th", null, "描述")
    ])
  ]),
  /* @__PURE__ */ d("tbody")
], -1);
function yr(e, t, l, a, n, o) {
  return s(), h("div", null, [
    d("div", Vo, [
      d("table", No, [
        jo,
        d("tbody", null, [
          d("tr", null, [
            Lo,
            d("td", Mo, [
              d("a", {
                style: { cursor: "pointer" },
                onClick: t[0] || (t[0] = (...r) => o.restart && o.restart(...r))
              }, "重启应用系统"),
              I("     " + w(n.model.rawUrl), 1)
            ]),
            Ro,
            d("td", Bo, w(n.model.contentRootPath), 1)
          ]),
          d("tr", null, [
            zo,
            d("td", qo, [
              d("span", Ho, w(n.model.host), 1),
              I("， "),
              d("span", xo, w(n.model.localHost), 1),
              I("  "),
              d("span", Ko, w(n.model.remoteHost), 1)
            ]),
            Wo,
            d("td", Go, [
              d("span", {
                title: n.model.commandLine
              }, w(n.model.processName), 9, Yo)
            ])
          ]),
          d("tr", null, [
            Jo,
            d("td", Zo, w(n.model.curDomainFriendlyName) + " ", 1),
            Qo,
            d("td", Xo, w(n.model.envVersion) + "  " + w(n.model.frameworkName), 1)
          ]),
          d("tr", null, [
            er,
            d("td", {
              class: "value",
              title: n.model.guid
            }, w(n.model.oSName) + " " + w(n.model.oSVersion), 9, tr),
            lr,
            d("td", {
              class: "value",
              title: n.model.uuid
            }, [
              n.model.product !== void 0 ? (s(), h("span", nr, w(n.model.product) + "，", 1)) : T("", !0),
              I(" " + w(n.model.userName + "/" + n.model.machineName), 1)
            ], 8, ar)
          ]),
          d("tr", null, [
            or,
            d("td", {
              class: "value",
              title: n.model.cpuID
            }, [
              I(w(n.model.processor) + "， " + w(n.model.processorCount) + " 核心，" + w(n.model.cpuRate) + " ", 1),
              n.model.temperature > 0 ? (s(), h("span", sr, "，" + w(n.model.temperature) + " ℃", 1)) : T("", !0)
            ], 8, rr),
            ir,
            d("td", ur, w(n.model.dateTimeNow) + "，开机" + w(n.model.uptime), 1)
          ]),
          d("tr", null, [
            dr,
            d("td", cr, [
              I(" 物理：" + w(n.model.availableMemory) + "M / " + w(n.model.memory) + "M， 工作/提交: " + w(n.model.workingSet64) + "M/" + w(n.model.privateMemorySize64) + "M GC: " + w(n.model.totalMemory) + "M ", 1),
              d("a", {
                onClick: t[1] || (t[1] = (...r) => o.memoryFree && o.memoryFree(...r)),
                title: "点击释放进程内存"
              }, "释放内存")
            ]),
            mr,
            pr
          ]),
          d("tr", null, [
            hr,
            d("td", fr, [
              d("a", {
                onClick: t[2] || (t[2] = (r) => o.main("Session")),
                target: "_blank",
                title: "点击打开Session列表"
              }, "Session列表")
            ]),
            gr,
            _r
          ])
        ])
      ]),
      br
    ])
  ]);
}
const vr = /* @__PURE__ */ D(Po, [["render", yr]]), wr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: vr
}, Symbol.toStringTag, { value: "Module" })), Ir = k({
  data() {
    return {
      form: {},
      typeMap: { Add: "新增", Detail: "查看", Edit: "编辑" },
      fields: [
        {
          name: "id",
          displayName: "编号",
          dataType: "Int32",
          description: "编号"
        },
        {
          name: "name",
          displayName: "名称",
          dataType: "String",
          length: 50,
          description: "名称"
        },
        {
          name: "displayName",
          displayName: "显示名",
          dataType: "String",
          length: 50,
          description: "显示名"
        },
        {
          name: "fullName",
          displayName: "全名",
          dataType: "String",
          length: 200,
          description: "全名"
        },
        {
          name: "parentID",
          displayName: "父编号",
          dataType: "Int32",
          description: "父编号"
        },
        {
          name: "url",
          displayName: "链接",
          dataType: "String",
          length: 200,
          description: "链接"
        },
        {
          name: "sort",
          displayName: "排序",
          dataType: "Int32",
          description: "排序"
        },
        {
          name: "icon",
          displayName: "图标",
          dataType: "String",
          length: 50,
          description: "图标"
        },
        {
          name: "visible",
          displayName: "可见",
          dataType: "Boolean",
          description: "可见"
        },
        {
          name: "necessary",
          displayName: "必要",
          dataType: "Boolean",
          description: "必要。必要的菜单，必须至少有角色拥有这些权限，如果没有则自动授权给系统角色"
        },
        {
          name: "permission",
          displayName: "权限子项",
          dataType: "String",
          length: 200,
          description: "权限子项。逗号分隔，每个权限子项名值竖线分隔"
        },
        {
          name: "ex1",
          displayName: "扩展1",
          dataType: "Int32",
          description: "扩展1"
        },
        {
          name: "ex2",
          displayName: "扩展2",
          dataType: "Int32",
          description: "扩展2"
        },
        {
          name: "ex3",
          displayName: "扩展3",
          dataType: "Double",
          description: "扩展3"
        },
        {
          name: "ex4",
          displayName: "扩展4",
          dataType: "String",
          length: 50,
          description: "扩展4"
        },
        {
          name: "ex5",
          displayName: "扩展5",
          dataType: "String",
          length: 50,
          description: "扩展5"
        },
        {
          name: "ex6",
          displayName: "扩展6",
          dataType: "String",
          length: 50,
          description: "扩展6"
        },
        {
          name: "remark",
          displayName: "备注",
          dataType: "String",
          length: 500,
          description: "备注"
        }
      ]
    };
  },
  computed: {
    id() {
      return this.$route.params.id;
    },
    currentPath() {
      let e = this, t = `/${e.type}${e.id === void 0 ? "" : "/" + e.id}`;
      return this.$route.path.replace(t, "");
    },
    type() {
      return this.$route.params.type;
    },
    isAdd() {
      return this.type === "Add";
    },
    isDetail() {
      return this.type === "Detail";
    }
  },
  // watch: {
  //   $route: {
  //     handler: function () {
  //       this.init()
  //     },
  //     immediate: true
  //   }
  // },
  created() {
    this.init();
  },
  activated() {
    this.init();
  },
  methods: {
    init() {
      this.isAdd || this.query();
    },
    getColumns() {
      let e = this, t = e.currentPath;
      e.$api.base.getColumns(t).then((l) => {
        e.fields = l.data;
      });
    },
    query() {
      let e = this;
      e.isDetail ? e.$api.base.getDetailData(e.currentPath, e.id).then((t) => {
        e.form = t.data;
      }) : e.$api.base.getData(e.currentPath, e.id).then((t) => {
        e.form = t.data;
      });
    },
    confirm() {
      let e = this;
      e.isAdd ? e.$api.base.add(e.currentPath, e.form).then(() => {
        e.$message({
          message: "新增成功",
          type: "success",
          duration: 5 * 1e3
        });
      }) : e.$api.base.edit(e.currentPath, e.form).then(() => {
        e.$message({
          message: "保存成功",
          type: "success",
          duration: 5 * 1e3
        });
      });
    },
    returnIndex() {
      this.$router.push(this.currentPath);
    }
  }
});
const Tr = { style: { position: "fixed", margin: "20px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } };
function $r(e, t, l, a, n, o) {
  const r = m("el-switch"), c = m("el-date-picker"), _ = m("el-input"), b = m("el-form-item"), $ = m("el-button"), y = m("el-form");
  return s(), h("div", null, [
    d("div", null, w(e.typeMap[e.type]), 1),
    i(y, {
      ref: "form",
      modelValue: e.form,
      "onUpdate:modelValue": t[0] || (t[0] = (p) => e.form = p),
      "label-position": "right",
      "label-width": "120px",
      inline: !0,
      class: "form-container"
    }, {
      default: u(() => [
        (s(!0), h(S, null, U(e.fields, (p, f) => (s(), h(S, null, [
          p.name.toLowerCase() != "id" ? (s(), v(b, {
            key: f,
            prop: p.name,
            label: p.displayName || p.name
          }, {
            default: u(() => [
              p.dataType == "Boolean" ? (s(), v(r, {
                key: 0,
                modelValue: e.form[p.name],
                "onUpdate:modelValue": (g) => e.form[p.name] = g,
                "active-color": "#13ce66",
                "inactive-color": "#ff4949"
              }, null, 8, ["modelValue", "onUpdate:modelValue"])) : p.dataType == "DateTime" ? (s(), v(c, {
                key: 1,
                modelValue: e.form[p.name],
                "onUpdate:modelValue": (g) => e.form[p.name] = g,
                type: "datetime",
                format: "YYYY-MM-DD HH:mm:ss",
                "value-format": "YYYY-MM-DD HH:mm:ss",
                placeholder: "选择日期时间"
              }, null, 8, ["modelValue", "onUpdate:modelValue"])) : p.dataType == "String" && p.length > 50 ? (s(), v(_, {
                key: 2,
                modelValue: e.form[p.name],
                "onUpdate:modelValue": (g) => e.form[p.name] = g,
                autosize: "",
                type: "textarea"
              }, null, 8, ["modelValue", "onUpdate:modelValue"])) : (s(), v(_, {
                key: 3,
                modelValue: e.form[p.name],
                "onUpdate:modelValue": (g) => e.form[p.name] = g,
                type: "text"
              }, null, 8, ["modelValue", "onUpdate:modelValue"]))
            ]),
            _: 2
          }, 1032, ["prop", "label"])) : T("", !0)
        ], 64))), 256)),
        e.isDetail ? T("", !0) : (s(), v(b, { key: 0 }, {
          default: u(() => [
            d("div", Tr, [
              i($, { onClick: e.returnIndex }, {
                default: u(() => [
                  I("取消")
                ]),
                _: 1
              }, 8, ["onClick"]),
              i($, {
                type: "primary",
                onClick: e.confirm
              }, {
                default: u(() => [
                  I("保存")
                ]),
                _: 1
              }, 8, ["onClick"])
            ])
          ]),
          _: 1
        }))
      ]),
      _: 1
    }, 8, ["modelValue"])
  ]);
}
const Sr = /* @__PURE__ */ D(Ir, [["render", $r], ["__scopeId", "data-v-2f2e012e"]]), Cr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Sr
}, Symbol.toStringTag, { value: "Module" })), Fr = k({
  name: "MenuList",
  data() {
    return {
      tableData: [],
      tableHeight: "300px",
      queryParams: {
        Q: null,
        dateRange: null
      },
      page: {
        pageIndex: 1,
        pageSize: 20,
        totalCount: 0
      },
      listLoading: !1,
      permissionFlags: {
        none: 0,
        detail: 1,
        insert: 2,
        update: 4,
        delete: 8
      }
    };
  },
  computed: {
    currentPath() {
      return this.$route.path;
    },
    queryData() {
      const e = this, t = e.queryParams.dateRange;
      t ? (e.queryParams.dtStart = t[0], e.queryParams.dtEnd = t[1]) : (e.queryParams.dtStart = null, e.queryParams.dtEnd = null);
      const l = {};
      return Object.assign(l, e.page, e.queryParams), l.dateRange = void 0, l;
    }
  },
  // watch: {
  //   $route: {
  //     handler: function () {
  //       this.init()
  //     },
  //     immediate: true
  //   }
  // },
  created() {
    this.init();
  },
  activated() {
    this.init();
  },
  methods: {
    init() {
      this.setQueryParams(), this.query();
    },
    setQueryParams() {
      const e = this;
      for (const t in e.$route.query)
        if (Object.hasOwnProperty.call(e.$route.query, t)) {
          const l = e.$route.query[t];
          e.queryParams[t] = l;
        }
    },
    getUrl(e, t) {
      const l = /{(\w+)}/g;
      return e.cellUrl.replace(l, (a, n) => t[n]);
    },
    add() {
      const e = this;
      e.$router.push(e.currentPath + "/Add");
    },
    detail(e) {
      const t = this;
      t.$router.push(t.currentPath + "/Detail/" + e.id);
    },
    editData(e) {
      const t = this;
      t.$router.push(t.currentPath + "/Edit/" + e.id);
    },
    deleteData(e) {
      const t = this;
      t.$api.base.deleteById(t.currentPath, e.id).then(() => {
        t.getTableData();
      });
    },
    query() {
      this.page.pageIndex = 1, this.getTableData();
    },
    getTableData() {
      const e = this;
      e.listLoading = !0, e.$api.base.getDataList(e.currentPath, e.queryData).then((t) => {
        e.listLoading = !1;
        const l = e.getTreeData(t.data);
        e.tableData = l, e.page = t.pager, e.page.totalCount = parseInt(e.page.totalCount), e.setTableHeight(e.tableData.length);
      });
    },
    getTreeData(e, t = 0) {
      const l = this;
      if (!e || e.length < 1)
        return [];
      const a = e.length, n = [];
      for (let o = 0; o < a; o++) {
        const r = e[o];
        if (r.parentID === t) {
          r.id, r.name, r.displayName, r.parentID, n.push(r);
          const c = l.getTreeData(e, r.id);
          c.length > 0 && (r.children = c);
        }
      }
      return n;
    },
    currentChange(e) {
      this.page.pageIndex = e, this.getTableData();
    },
    handleSizeChange(e) {
      this.page.pageSize = e, this.getTableData();
    },
    sortChange({ column: e, prop: t, order: l }) {
      l === "ascending" ? (this.page.desc = !1, this.page.sort = t) : l === "descending" ? (this.page.desc = !0, this.page.sort = t) : (this.page.desc = !0, this.page.sort = void 0), this.getTableData();
    },
    rowDblclick(e) {
      this.editData(e);
    },
    hasPermission(e) {
      const t = this, l = t.$route.meta.menuId, a = t.$route.meta.permissions;
      return t.$store.state.user.hasPermission(t.$store, {
        menuId: l,
        actionId: e,
        permissions: a
      });
    },
    setTableHeight(e) {
      const t = this;
      e && e > 0 && (e > 20 ? e = 20 : e < 8 && (e = 9), setTimeout(() => {
        t.tableHeight = e * 35.9 + "px";
      }, 500));
    }
  }
});
const Dr = { class: "list-container" }, kr = { class: "table-container" };
function Ur(e, t, l, a, n, o) {
  const r = m("el-button"), c = m("el-col"), _ = m("el-row"), b = m("el-table-column"), $ = m("el-switch"), y = m("el-table"), p = Re("loading");
  return s(), h("div", Dr, [
    i(_, {
      type: "flex",
      class: "search",
      justify: "end"
    }, {
      default: u(() => [
        e.hasPermission(e.permissionFlags.insert) ? (s(), v(c, {
          key: 0,
          span: 4,
          class: "left-search"
        }, {
          default: u(() => [
            i(r, {
              type: "primary",
              onClick: e.add
            }, {
              default: u(() => [
                I("新增")
              ]),
              _: 1
            }, 8, ["onClick"])
          ]),
          _: 1
        })) : T("", !0),
        i(c, {
          span: 20,
          class: "right-search"
        })
      ]),
      _: 1
    }),
    d("div", kr, [
      A((s(), v(y, {
        data: e.tableData,
        stripe: "",
        border: "",
        onSortChange: e.sortChange,
        onRowDblclick: e.rowDblclick,
        "tree-props": {
          children: "children",
          hasChildren: "hasChildren"
        },
        "row-key": "id",
        "default-expand-all": ""
      }, {
        default: u(() => [
          i(b, {
            prop: "name",
            label: "节点名",
            width: "180"
          }),
          i(b, {
            prop: "displayName",
            label: "显示名",
            width: "180"
          }),
          i(b, {
            prop: "url",
            label: "链接",
            width: "250"
          }),
          i(b, {
            prop: "sort",
            label: "排序",
            width: "50"
          }),
          i(b, {
            align: "center",
            prop: "visible",
            label: "可见",
            width: "80"
          }, {
            default: u((f) => [
              i($, {
                modelValue: f.row.visible,
                "onUpdate:modelValue": (g) => f.row.visible = g,
                "active-color": "#13ce66",
                "inactive-color": "#ff4949",
                disabled: ""
              }, null, 8, ["modelValue", "onUpdate:modelValue"])
            ]),
            _: 1
          }),
          i(b, {
            align: "center",
            prop: "necessary",
            label: "必要",
            width: "80"
          }, {
            default: u((f) => [
              i($, {
                value: f.row.necessary,
                "active-color": "#13ce66",
                "inactive-color": "#ff4949"
              }, null, 8, ["value"])
            ]),
            _: 1
          }),
          i(b, {
            prop: "permission",
            label: "权限子项",
            width: "250"
          }),
          i(b, {
            label: "操作",
            align: "center",
            width: "140",
            "class-name": "small-padding fixed-width"
          }, {
            default: u((f) => [
              !e.hasPermission(e.permissionFlags.update) && e.hasPermission(e.permissionFlags.detail) ? (s(), v(r, {
                key: 0,
                type: "primary",
                size: "small",
                onClick: (g) => e.detail(f.row)
              }, {
                default: u(() => [
                  I(" 查看 ")
                ]),
                _: 2
              }, 1032, ["onClick"])) : T("", !0),
              e.hasPermission(e.permissionFlags.update) ? (s(), v(r, {
                key: 1,
                type: "primary",
                size: "small",
                onClick: (g) => e.editData(f.row)
              }, {
                default: u(() => [
                  I(" 编辑 ")
                ]),
                _: 2
              }, 1032, ["onClick"])) : T("", !0),
              e.hasPermission(e.permissionFlags.delete) ? (s(), v(r, {
                key: 2,
                size: "small",
                type: "danger",
                onClick: (g) => e.deleteData(f.row)
              }, {
                default: u(() => [
                  I(" 删除 ")
                ]),
                _: 2
              }, 1032, ["onClick"])) : T("", !0)
            ]),
            _: 1
          })
        ]),
        _: 1
      }, 8, ["data", "onSortChange", "onRowDblclick"])), [
        [p, e.listLoading]
      ])
    ])
  ]);
}
const Or = /* @__PURE__ */ D(Fr, [["render", Ur], ["__scopeId", "data-v-7c0d85a2"]]), Er = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Or
}, Symbol.toStringTag, { value: "Module" })), Ar = [{
  itemType: "datePicker",
  name: "dtStart$dtEnd",
  displayName: "时间范围",
  showInSearch: !0,
  options: {
    type: "daterange",
    setDefaultValue: !1
  }
}, {
  name: "Q",
  displayName: "",
  showInSearch: !0,
  options: {
    placeholder: "请输入关键字"
  }
}], Pr = [{
  name: "id",
  displayName: "编号",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "编号",
  showInList: !0,
  width: "95"
}, {
  name: "name",
  displayName: "名称",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !1,
  isDataObjectField: !0,
  description: "名称",
  showInList: !0,
  width: "95"
}, {
  name: "enable",
  displayName: "启用",
  dataType: "Boolean",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "启用",
  showInList: !0,
  width: "95"
}, {
  name: "isSystem",
  displayName: "系统",
  dataType: "Boolean",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "系统。用于业务系统开发使用，不受数据权限约束，禁止修改名称或删除",
  showInList: !0,
  width: "95"
}, {
  name: "permission",
  displayName: "权限",
  dataType: "String",
  itemType: null,
  length: -1,
  nullable: !0,
  isDataObjectField: !0,
  description: "权限。对不同资源的权限，逗号分隔，每个资源的权限子项竖线分隔",
  showInList: !1,
  width: "95"
}, {
  name: "sort",
  displayName: "排序",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "排序",
  showInList: !0,
  width: "95"
}, {
  name: "ex1",
  displayName: "扩展1",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展1",
  showInList: !0,
  width: "105"
}, {
  name: "ex2",
  displayName: "扩展2",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展2",
  showInList: !0,
  width: "105"
}, {
  name: "ex3",
  displayName: "扩展3",
  dataType: "Double",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展3",
  showInList: !0,
  width: "105"
}, {
  name: "ex4",
  displayName: "扩展4",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展4",
  showInList: !0,
  width: "105"
}, {
  name: "ex5",
  displayName: "扩展5",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展5",
  showInList: !0,
  width: "105"
}, {
  name: "ex6",
  displayName: "扩展6",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展6",
  showInList: !0,
  width: "105"
}, {
  name: "createUser",
  displayName: "创建者",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "创建者",
  showInList: !0,
  width: "105"
}, {
  name: "createUserID",
  displayName: "创建用户",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "创建用户",
  showInList: !0,
  width: "120"
}, {
  name: "createIP",
  displayName: "创建地址",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "创建地址",
  showInList: !0,
  width: "120"
}, {
  name: "createTime",
  displayName: "创建时间",
  dataType: "DateTime",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "创建时间",
  showInList: !0,
  width: "155"
}, {
  name: "updateUser",
  displayName: "更新者",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "更新者",
  showInList: !0,
  width: "105"
}, {
  name: "updateUserID",
  displayName: "更新用户",
  dataType: "Int32",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "更新用户",
  showInList: !0,
  width: "120"
}, {
  name: "updateIP",
  displayName: "更新地址",
  dataType: "String",
  itemType: null,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "更新地址",
  showInList: !0,
  width: "120"
}, {
  name: "updateTime",
  displayName: "更新时间",
  dataType: "DateTime",
  itemType: null,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "更新时间",
  showInList: !0,
  width: "155"
}, {
  name: "remark",
  displayName: "备注",
  dataType: "String",
  itemType: null,
  length: 500,
  nullable: !0,
  isDataObjectField: !0,
  description: "备注",
  showInList: !0,
  width: "95"
}], Vr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  tableColumnConfig: Pr,
  tableSearchConfig: Ar
}, Symbol.toStringTag, { value: "Module" })), Nr = k({
  data() {
    return {
      form: {},
      fields: [],
      imObj: {},
      // 父级全选半选状态
      typeMap: { Add: "新增", Detail: "查看", Edit: "编辑" }
    };
  },
  computed: {
    id() {
      return this.$route.params.id;
    },
    currentPath() {
      const e = this, t = `/${e.type}${e.id === void 0 ? "" : "/" + e.id}`;
      return this.$route.path.replace(t, "");
    },
    type() {
      return this.$route.params.type;
    },
    isAdd() {
      return this.type === "Add";
    },
    isDetail() {
      return this.type === "Detail";
    },
    rolePermissions() {
      const t = this.form.permission, l = {};
      if (!t)
        return l;
      const a = t.split(",");
      for (const n in a)
        if (Object.prototype.hasOwnProperty.call(a, n)) {
          const r = a[n].split("#");
          l[r[0]] = r[1];
        }
      return l;
    },
    tableData() {
      const t = this.$store.getters.menuRouters, l = [];
      return t.map((a) => {
        const n = {
          id: a.id,
          name: a.name,
          displayName: a.displayName,
          permissions: a.permissions,
          parentID: a.parentID
        };
        a.hasChildren && (n.children = [], a.children.map((o) => {
          if (o.isFormRoute)
            return;
          const r = {
            id: o.id,
            name: o.name,
            displayName: o.displayName,
            permissions: o.permissions,
            parentID: o.parentID
          };
          n.children.push(r);
        })), l.push(n);
      }), l;
    }
  },
  // watch: {
  //   $route: {
  //     handler: function () {
  //       this.init()
  //     },
  //     immediate: true
  //   }
  // },
  created() {
    this.init();
  },
  activated() {
    this.init();
  },
  methods: {
    init() {
      this.isAdd || this.query();
    },
    query() {
      const e = this;
      e.$api.base.getData(e.currentPath, e.id).then((t) => {
        e.form = t.data, e.allCheckUpdate();
      });
    },
    confirm() {
      const e = this;
      e.isAdd ? e.$api.base.add(e.currentPath, e.form).then(() => {
        e.$message({
          message: "新增成功",
          type: "success",
          duration: 5 * 1e3
        }), e.$router.go(-1);
      }) : e.$api.base.edit(e.currentPath, e.form).then(() => {
        e.$message({
          message: "保存成功",
          type: "success",
          duration: 5 * 1e3
        }), e.$router.go(-1);
      });
    },
    returnIndex() {
      this.$router.push(this.currentPath);
    },
    checkChange({ id: e, permissions: t, parentID: l }) {
      const a = this;
      let n = !1, o = 0;
      for (const r in t)
        if (Object.prototype.hasOwnProperty.call(t, r)) {
          const c = a.form["pf" + e + "_" + r];
          c && (o = o + 1), n = n || c;
        }
      a.form["p" + e] = n, a.imObj[e] = o > 0 && o < Object.entries(t).length, a.parentCheckUpdate(l);
    },
    checkAllChange({ id: e, permissions: t, parentID: l }) {
      const a = this, n = a.form["p" + e];
      for (const o in t)
        Object.prototype.hasOwnProperty.call(t, o) && (a.form["pf" + e + "_" + o] = n);
      a.imObj[e] = !1, a.parentCheckUpdate(l);
    },
    parentCheckAllChange({ id: e, children: t }) {
      const l = this, a = l.form["p" + e];
      t.forEach((n) => {
        l.form["p" + n.id] = a, l.checkAllChange({
          id: n.id,
          permissions: n.permissions,
          parentID: n.parentID
        });
      }), l.imObj[e] = !1;
    },
    parentCheckUpdate(e) {
      const t = this;
      let l = !1, a = !1;
      t.tableData.find((o) => o.id === e).children.forEach((o) => {
        const r = t.form["p" + o.id], c = t.imObj[o.id];
        l = l || r || !1, a = a || !r || c || !1;
      }), l || (a = !1), t.form["p" + e] = l, t.imObj[e] = a;
    },
    roCheck({ id: e, children: t }) {
      const l = this, a = l.form["pc_readonly_" + e];
      t.forEach((n) => {
        l.form["pf" + n.id + "_" + 1] = a, l.checkChange(n);
      });
    },
    allCheckUpdate() {
      const e = this;
      e.tableData.map((t) => {
        if (t.permissions) {
          for (const l in t.permissions)
            if (Object.prototype.hasOwnProperty.call(t.permissions, l)) {
              const a = (
                // tslint:disable-next-line:no-bitwise
                (parseInt(l, 10) & parseInt(e.rolePermissions[t.id], 10)) !== 0
              );
              e.form["pf" + t.id + "_" + l] = a;
            }
        }
        t.children && t.children.map((l) => {
          for (const a in l.permissions)
            if (Object.prototype.hasOwnProperty.call(l.permissions, a)) {
              l.permissions[a];
              const n = (
                // tslint:disable-next-line:no-bitwise
                (parseInt(a, 10) & parseInt(e.rolePermissions[l.id], 10)) !== 0
              );
              e.form["pf" + l.id + "_" + parseInt(a, 10)] = n;
            }
          e.checkChange(l);
        });
      });
    }
  }
});
const jr = { style: { position: "fixed", margin: "20px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } };
function Lr(e, t, l, a, n, o) {
  const r = m("el-input"), c = m("el-form-item"), _ = m("el-switch"), b = m("el-button"), $ = m("el-table-column"), y = m("el-checkbox"), p = m("el-table"), f = m("el-form");
  return s(), h("div", null, [
    d("div", null, w(e.typeMap[e.type]), 1),
    i(f, {
      ref: "form",
      modelValue: e.form,
      "onUpdate:modelValue": t[4] || (t[4] = (g) => e.form = g),
      "label-position": "right",
      "label-width": "120px",
      inline: !0,
      class: "form-container"
    }, {
      default: u(() => [
        i(c, {
          label: "名称",
          prop: "name"
        }, {
          default: u(() => [
            i(r, {
              modelValue: e.form.name,
              "onUpdate:modelValue": t[0] || (t[0] = (g) => e.form.name = g)
            }, null, 8, ["modelValue"])
          ]),
          _: 1
        }),
        i(c, {
          label: "启用",
          prop: "enable"
        }, {
          default: u(() => [
            i(_, {
              modelValue: e.form.enable,
              "onUpdate:modelValue": t[1] || (t[1] = (g) => e.form.enable = g),
              "active-color": "#13ce66",
              "inactive-color": "#ff4949"
            }, null, 8, ["modelValue"])
          ]),
          _: 1
        }),
        i(c, {
          label: "系统",
          prop: "isSystem"
        }, {
          default: u(() => [
            i(_, {
              modelValue: e.form.isSystem,
              "onUpdate:modelValue": t[2] || (t[2] = (g) => e.form.isSystem = g),
              "active-color": "#13ce66",
              "inactive-color": "#ff4949"
            }, null, 8, ["modelValue"])
          ]),
          _: 1
        }),
        i(c, {
          label: "备注",
          prop: "remark"
        }, {
          default: u(() => [
            i(r, {
              rows: 4,
              type: "textarea",
              modelValue: e.form.remark,
              "onUpdate:modelValue": t[3] || (t[3] = (g) => e.form.remark = g)
            }, null, 8, ["modelValue"])
          ]),
          _: 1
        }),
        i(c, null, {
          default: u(() => [
            d("div", jr, [
              i(b, { onClick: e.returnIndex }, {
                default: u(() => [
                  I("返回")
                ]),
                _: 1
              }, 8, ["onClick"]),
              i(b, {
                type: "primary",
                onClick: e.confirm
              }, {
                default: u(() => [
                  I("保存")
                ]),
                _: 1
              }, 8, ["onClick"])
            ])
          ]),
          _: 1
        }),
        e.isAdd ? T("", !0) : (s(), v(p, {
          key: 0,
          data: e.tableData,
          "tree-props": {
            children: "children",
            hasChildren: "hasChildren"
          },
          "row-key": "id",
          border: "",
          "default-expand-all": ""
        }, {
          default: u(() => [
            I(" > "),
            i($, {
              prop: "name",
              label: "名称",
              width: "200"
            }),
            i($, {
              prop: "displayName",
              label: "显示名",
              width: "100"
            }),
            i($, { label: "操作" }, {
              default: u((g) => [
                Object.entries(g.row.permissions).length > 0 ? (s(), h(S, { key: 0 }, [
                  i(y, {
                    indeterminate: e.imObj[g.row.id],
                    modelValue: e.form["p" + g.row.id],
                    "onUpdate:modelValue": (C) => e.form["p" + g.row.id] = C,
                    onChange: (C) => e.checkAllChange(g.row)
                  }, {
                    default: u(() => [
                      I(" 全选 ")
                    ]),
                    _: 2
                  }, 1032, ["indeterminate", "modelValue", "onUpdate:modelValue", "onChange"]),
                  (s(!0), h(S, null, U(g.row.permissions, (C, F) => (s(), v(y, {
                    key: g.row.id + "" + F,
                    label: C,
                    modelValue: e.form["pf" + g.row.id + "_" + F],
                    "onUpdate:modelValue": (V) => e.form["pf" + g.row.id + "_" + F] = V,
                    onChange: (V) => e.checkChange(g.row)
                  }, null, 8, ["label", "modelValue", "onUpdate:modelValue", "onChange"]))), 128))
                ], 64)) : (s(), h(S, { key: 1 }, [
                  i(y, {
                    indeterminate: e.imObj[g.row.id],
                    modelValue: e.form["p" + g.row.id],
                    "onUpdate:modelValue": (C) => e.form["p" + g.row.id] = C,
                    onChange: (C) => e.parentCheckAllChange(g.row)
                  }, {
                    default: u(() => [
                      I(" 全选 ")
                    ]),
                    _: 2
                  }, 1032, ["indeterminate", "modelValue", "onUpdate:modelValue", "onChange"]),
                  i(y, {
                    modelValue: e.form["pc_readonly_" + g.row.id],
                    "onUpdate:modelValue": (C) => e.form["pc_readonly_" + g.row.id] = C,
                    onChange: (C) => e.roCheck(g.row)
                  }, {
                    default: u(() => [
                      I(" 只读 ")
                    ]),
                    _: 2
                  }, 1032, ["modelValue", "onUpdate:modelValue", "onChange"])
                ], 64))
              ]),
              _: 1
            })
          ]),
          _: 1
        }, 8, ["data"]))
      ]),
      _: 1
    }, 8, ["modelValue"])
  ]);
}
const Mr = /* @__PURE__ */ D(Nr, [["render", Lr], ["__scopeId", "data-v-89ec331d"]]), Rr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Mr
}, Symbol.toStringTag, { value: "Module" })), Br = [{
  name: "dtStart$dtEnd",
  displayName: "时间范围",
  showInSearch: !0,
  itemType: "datePicker",
  options: {
    type: "daterange",
    setDefaultValue: !1
  }
}, {
  name: "roleIds",
  displayName: "角色",
  showInSearch: !0,
  itemType: "select",
  url: "/Admin/Role?enable=true",
  //  [
  //   { label: '管理员', value: 1 },
  //   { label: '高级用户', value: 2 },
  //   { label: '普通用户', value: 3 },
  //   { label: '游客', value: 4 },
  //   { label: '新生命', value: 5 },
  // ],
  options: {
    labelField: "name",
    valueField: "id"
  }
}, {
  name: "Q",
  displayName: "",
  showInSearch: !0,
  options: {
    placeholder: "请输入关键字"
  }
}], zr = [{
  name: "新增",
  handler: "add",
  type: "primary"
}], J = {
  none: 0,
  detail: 1,
  insert: 2,
  update: 4,
  delete: 8
}, qr = [{
  name: "handler",
  displayName: "操作",
  width: "155px",
  showInList: !0,
  handlerList: [{
    innerText: "查看",
    handler: "detail",
    if: (e) => !e.hasPermission(J.update) && e.hasPermission(J.detail)
  }, {
    innerText: "编辑",
    handler: "editData",
    type: "primary",
    if: (e) => e.hasPermission(J.update)
  }, {
    innerText: "删除",
    type: "danger",
    handler: "deleteData",
    if: (e) => e.hasPermission(J.delete)
  }]
}], Hr = [{
  tableName: "User",
  id: 28,
  tableId: 3,
  name: "id",
  displayName: "编号",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !0,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "编号",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 1,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 29,
  tableId: 3,
  name: "name",
  displayName: "名称",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !0,
  length: 50,
  nullable: !1,
  isDataObjectField: !0,
  description: "名称。登录用户名",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 2,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 31,
  tableId: 3,
  name: "displayName",
  displayName: "昵称",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "昵称",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 4,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 32,
  tableId: 3,
  name: "sex",
  displayName: "性别",
  enable: !0,
  dataType: "SexKinds",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "性别。未知、男、女",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 5,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 33,
  tableId: 3,
  name: "mail",
  displayName: "邮件",
  enable: !0,
  dataType: "String",
  itemType: "mail",
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "邮件",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 6,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 34,
  tableId: 3,
  name: "mobile",
  displayName: "手机",
  enable: !0,
  dataType: "String",
  itemType: "mobile",
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "手机",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 7,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 35,
  tableId: 3,
  name: "code",
  displayName: "代码",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "代码。身份证、员工编号等",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 8,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 36,
  tableId: 3,
  name: "areaId",
  displayName: "地区",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "地区。省市区",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 9,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 37,
  tableId: 3,
  name: "avatar",
  displayName: "头像",
  enable: !0,
  dataType: "String",
  itemType: "image",
  primaryKey: !1,
  master: !1,
  length: 200,
  nullable: !0,
  isDataObjectField: !0,
  description: "头像",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 10,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 38,
  tableId: 3,
  name: "roleID",
  displayName: "角色",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "角色。主要角色",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 11,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 39,
  tableId: 3,
  name: "roleIds",
  displayName: "角色组",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 200,
  nullable: !0,
  isDataObjectField: !0,
  description: "角色组。次要角色集合",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 12,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 40,
  tableId: 3,
  name: "departmentID",
  displayName: "部门",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "部门。组织机构",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 13,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 41,
  tableId: 3,
  name: "online",
  displayName: "在线",
  enable: !0,
  dataType: "Boolean",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "在线",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 14,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 42,
  tableId: 3,
  name: "enable",
  displayName: "启用",
  enable: !0,
  dataType: "Boolean",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "启用",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 15,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 43,
  tableId: 3,
  name: "age",
  displayName: "年龄",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "年龄。周岁",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 16,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 44,
  tableId: 3,
  name: "birthday",
  displayName: "生日",
  enable: !0,
  dataType: "DateTime",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !0,
  isDataObjectField: !0,
  description: "生日。公历年月日",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 17,
  width: "155",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 45,
  tableId: 3,
  name: "logins",
  displayName: "登录次数",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "登录次数",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 18,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 46,
  tableId: 3,
  name: "lastLogin",
  displayName: "最后登录",
  enable: !0,
  dataType: "DateTime",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !0,
  isDataObjectField: !0,
  description: "最后登录",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 19,
  width: "155",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 47,
  tableId: 3,
  name: "lastLoginIP",
  displayName: "最后登录IP",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "最后登录IP",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 20,
  width: "130",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 48,
  tableId: 3,
  name: "registerTime",
  displayName: "注册时间",
  enable: !0,
  dataType: "DateTime",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !0,
  isDataObjectField: !0,
  description: "注册时间",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 21,
  width: "155",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 49,
  tableId: 3,
  name: "registerIP",
  displayName: "注册IP",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "注册IP",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 22,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 3,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 3,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 50,
  tableId: 3,
  name: "onlineTime",
  displayName: "在线时间",
  enable: !0,
  dataType: "Int32",
  itemType: "TimeSpan",
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "在线时间。累计在线总时间，秒",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 23,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 45,
  updateTime: "2022-12-27 14:43:22",
  updateIP: null
}, {
  tableName: "User",
  id: 51,
  tableId: 3,
  name: "ex1",
  displayName: "扩展1",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展1",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 24,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 52,
  tableId: 3,
  name: "ex2",
  displayName: "扩展2",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展2",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 25,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 53,
  tableId: 3,
  name: "ex3",
  displayName: "扩展3",
  enable: !0,
  dataType: "Double",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "扩展3",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 26,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 54,
  tableId: 3,
  name: "ex4",
  displayName: "扩展4",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展4",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 27,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 55,
  tableId: 3,
  name: "ex5",
  displayName: "扩展5",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展5",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 28,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 56,
  tableId: 3,
  name: "ex6",
  displayName: "扩展6",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "扩展6",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 29,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 57,
  tableId: 3,
  name: "updateUser",
  displayName: "更新者",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "更新者",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 30,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 58,
  tableId: 3,
  name: "updateUserID",
  displayName: "更新用户",
  enable: !0,
  dataType: "Int32",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "更新用户",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 31,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 59,
  tableId: 3,
  name: "updateIP",
  displayName: "更新地址",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 50,
  nullable: !0,
  isDataObjectField: !0,
  description: "更新地址",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 32,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 60,
  tableId: 3,
  name: "updateTime",
  displayName: "更新时间",
  enable: !0,
  dataType: "DateTime",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !0,
  description: "更新时间",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 33,
  width: "155",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 61,
  tableId: 3,
  name: "remark",
  displayName: "备注",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 500,
  nullable: !0,
  isDataObjectField: !0,
  description: "备注",
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 34,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 62,
  tableId: 3,
  name: "lastLoginAddress",
  displayName: "物理地址",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !1,
  description: null,
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 35,
  width: "120",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 63,
  tableId: 3,
  name: "departmentName",
  displayName: "部门",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !1,
  description: null,
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 36,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 64,
  tableId: 3,
  name: "areaName",
  displayName: "地区",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !1,
  description: null,
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 37,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 65,
  tableId: 3,
  name: "roleName",
  displayName: "角色",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !1,
  description: null,
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 38,
  width: "95",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}, {
  tableName: "User",
  id: 66,
  tableId: 3,
  name: "roleNames",
  displayName: "角色组",
  enable: !0,
  dataType: "String",
  itemType: null,
  primaryKey: !1,
  master: !1,
  length: 0,
  nullable: !1,
  isDataObjectField: !1,
  description: null,
  showInList: !0,
  showInAddForm: !0,
  showInEditForm: !0,
  showInDetailForm: !0,
  showInSearch: !1,
  sort: 39,
  width: "105",
  cellText: null,
  cellTitle: null,
  cellUrl: null,
  headerText: null,
  headerTitle: null,
  headerUrl: null,
  dataAction: null,
  dataSource: null,
  createUserId: 0,
  createTime: "2022-12-02 23:46:29",
  createIP: null,
  updateUserId: 0,
  updateTime: "2022-12-02 23:46:29",
  updateIP: null
}], xr = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  tableActionConfig: qr,
  tableColumnConfig: Hr,
  tableHandlerConfig: zr,
  tableSearchConfig: Br
}, Symbol.toStringTag, { value: "Module" })), Kr = k({
  props: ["path"],
  data() {
    return {
      form: {},
      form2: {},
      properties: [],
      activeName: "UserInfo"
    };
  },
  computed: {
    currentPath() {
      return this.path;
    }
  },
  watch: {
    $route: {
      handler: function() {
        this.init();
      },
      immediate: !0
    }
  },
  methods: {
    init() {
      this.query();
    },
    query() {
      let e = this;
      e.$api.user.getUserInfo().then((t) => {
        const l = t.data;
        e.$store.dispatch("setUserInfo", l), e.form = l;
      });
    },
    confirm() {
      let e = this;
      e.$api.user.updateUserInfo(e.form).then(() => {
        let t = "保存成功";
        e.$message({
          message: t,
          type: "success",
          duration: 3 * 1e3
        }), e.query();
      });
    },
    confirm2() {
      let e = this;
      e.$api.user.changePassword(e.form2).then(() => {
        let t = "保存成功";
        e.$message({
          message: t,
          type: "success",
          duration: 3 * 1e3
        }), e.form2 = {};
      });
    }
  }
});
const Wr = { class: "objform" }, Gr = ["src"], Yr = { style: { position: "fixed", margin: "30px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } }, Jr = { class: "objform" }, Zr = { style: { position: "fixed", margin: "30px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } };
function Qr(e, t, l, a, n, o) {
  const r = m("el-form-item"), c = m("el-input"), _ = m("el-option"), b = m("el-select"), $ = m("el-button"), y = m("el-form"), p = m("el-tab-pane"), f = m("el-tabs");
  return s(), v(f, {
    modelValue: e.activeName,
    "onUpdate:modelValue": t[9] || (t[9] = (g) => e.activeName = g)
  }, {
    default: u(() => [
      i(p, {
        label: "基本信息",
        name: "UserInfo"
      }, {
        default: u(() => [
          d("div", Wr, [
            i(y, {
              "label-position": "right",
              "label-width": "120px",
              ref: "form",
              model: e.form
            }, {
              default: u(() => [
                i(r, {
                  label: "头像",
                  prop: "avatar"
                }, {
                  default: u(() => [
                    d("img", {
                      style: { height: "100px", width: "100px" },
                      src: e.$store.getters.urls.baseUrl + e.form.avatar
                    }, null, 8, Gr)
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "名称",
                  prop: "name"
                }, {
                  default: u(() => [
                    i(c, {
                      modelValue: e.form.name,
                      "onUpdate:modelValue": t[0] || (t[0] = (g) => e.form.name = g),
                      disabled: ""
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "显示名",
                  prop: "displayName"
                }, {
                  default: u(() => [
                    i(c, {
                      modelValue: e.form.displayName,
                      "onUpdate:modelValue": t[1] || (t[1] = (g) => e.form.displayName = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "性别",
                  prop: "sex"
                }, {
                  default: u(() => [
                    i(b, {
                      modelValue: e.form.sex,
                      "onUpdate:modelValue": t[2] || (t[2] = (g) => e.form.sex = g),
                      filterable: ""
                    }, {
                      default: u(() => [
                        (s(), v(_, {
                          key: 0,
                          label: "未知",
                          value: 0
                        })),
                        (s(), v(_, {
                          key: 1,
                          label: "男",
                          value: 1
                        })),
                        (s(), v(_, {
                          key: 2,
                          label: "女",
                          value: -1
                        }))
                      ]),
                      _: 1
                    }, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "邮箱",
                  prop: "mail"
                }, {
                  default: u(() => [
                    i(c, {
                      modelValue: e.form.mail,
                      "onUpdate:modelValue": t[3] || (t[3] = (g) => e.form.mail = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "手机",
                  prop: "mobile"
                }, {
                  default: u(() => [
                    i(c, {
                      modelValue: e.form.mobile,
                      "onUpdate:modelValue": t[4] || (t[4] = (g) => e.form.mobile = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "代码",
                  prop: "code"
                }, {
                  default: u(() => [
                    i(c, {
                      modelValue: e.form.code,
                      "onUpdate:modelValue": t[5] || (t[5] = (g) => e.form.code = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "角色",
                  prop: "name"
                }, {
                  default: u(() => [
                    d("span", null, w(e.form.roleNames), 1)
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "登录次数",
                  prop: "name"
                }, {
                  default: u(() => [
                    d("span", null, w(e.form.logins), 1)
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "最后登录时间",
                  prop: "name"
                }, {
                  default: u(() => [
                    d("span", null, w(e.form.lastLogin), 1)
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "最后登录IP",
                  prop: "name"
                }, {
                  default: u(() => [
                    d("span", null, w(e.form.lastLoginIP), 1)
                  ]),
                  _: 1
                }),
                i(r, {
                  prop: "",
                  "label-name": ""
                }, {
                  default: u(() => [
                    d("div", Yr, [
                      i($, {
                        type: "primary",
                        onClick: e.confirm
                      }, {
                        default: u(() => [
                          I("保存")
                        ]),
                        _: 1
                      }, 8, ["onClick"])
                    ])
                  ]),
                  _: 1
                })
              ]),
              _: 1
            }, 8, ["model"])
          ])
        ]),
        _: 1
      }),
      i(p, {
        label: "修改密码",
        name: "ChangePassword"
      }, {
        default: u(() => [
          d("div", Jr, [
            i(y, {
              "label-position": "right",
              "label-width": "120px",
              ref: "form2",
              model: e.form2
            }, {
              default: u(() => [
                i(r, {
                  label: "旧密码",
                  prop: "oldPassword"
                }, {
                  default: u(() => [
                    i(c, {
                      type: "password",
                      modelValue: e.form2.oldPassword,
                      "onUpdate:modelValue": t[6] || (t[6] = (g) => e.form2.oldPassword = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "新密码",
                  prop: "newPassword"
                }, {
                  default: u(() => [
                    i(c, {
                      type: "password",
                      modelValue: e.form2.newPassword,
                      "onUpdate:modelValue": t[7] || (t[7] = (g) => e.form2.newPassword = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  label: "确认密码",
                  prop: "newPassword2"
                }, {
                  default: u(() => [
                    i(c, {
                      type: "password",
                      modelValue: e.form2.newPassword2,
                      "onUpdate:modelValue": t[8] || (t[8] = (g) => e.form2.newPassword2 = g)
                    }, null, 8, ["modelValue"])
                  ]),
                  _: 1
                }),
                i(r, {
                  prop: "",
                  "label-name": ""
                }, {
                  default: u(() => [
                    d("div", Zr, [
                      i($, {
                        type: "primary",
                        onClick: e.confirm2
                      }, {
                        default: u(() => [
                          I("保存")
                        ]),
                        _: 1
                      }, 8, ["onClick"])
                    ])
                  ]),
                  _: 1
                })
              ]),
              _: 1
            }, 8, ["model"])
          ])
        ]),
        _: 1
      }),
      i(p, {
        label: "第三方授权",
        name: "OAuthConfig"
      }, {
        default: u(() => [
          I("3")
        ]),
        _: 1
      })
    ]),
    _: 1
  }, 8, ["modelValue"]);
}
const Xr = /* @__PURE__ */ D(Kr, [["render", Qr], ["__scopeId", "data-v-c055cde1"]]), es = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: Xr
}, Symbol.toStringTag, { value: "Module" })), ts = k({
  name: "AuthRedirect",
  async created() {
    let e = this;
    const t = e.$route.query.redirect || "/";
    let l = e.$route.hash.replace("#token=", "");
    e.$store.dispatch("setToken", l), e.$api.user.getUserInfo().then((a) => {
      const n = a.data;
      e.$store.dispatch("setUserInfo", n);
    }), e.$api.menu.getMenu().then((a) => {
      const n = a.data;
      te(n), e.$store.dispatch("generateRoutes", n);
      const o = e.$store.getters.addRouters;
      o && o.forEach((r) => {
        e.$router.addRoute(r);
      }), e.$router.push({ path: t });
    });
  },
  render() {
    return "";
  }
}), ls = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ts
}, Symbol.toStringTag, { value: "Module" })), as = k({
  data() {
    return {
      loginForm: {
        username: null,
        password: null,
        remember: !0
      }
    };
  },
  computed: {
    sysConfig() {
      return this.$store.getters.sysConfig;
    },
    loginConfig() {
      let t = this.$store.getters.loginConfig;
      return t || (t = {
        displayName: "魔方",
        logo: "",
        // 系统logo
        allowLogin: !0,
        allowRegister: !0,
        providers: []
      }), t;
    },
    baseUrl() {
      return this.$store.getters.urls.baseUrl;
    },
    redirect() {
      return this.$route.query.redirect;
    },
    displayName() {
      let e = this;
      return e.sysConfig && e.sysConfig.displayName || e.loginConfig && e.loginConfig.displayName;
    }
  },
  created() {
    let e = this;
    try {
      e.$messageBox.close();
    } catch {
    }
    e.autoAuthRedirect(), e.$api.config.getLoginConfig().then((t) => {
      let l = t.data;
      e.$store.dispatch("setLoginConfig", l);
    });
  },
  methods: {
    login() {
      let e = this;
      e.$api.user.login(e.loginForm).then(async (t) => {
        let a = t.data.token;
        await e.$store.dispatch("setToken", a), e.$api.user.getUserInfo().then((n) => {
          const o = n.data;
          e.$store.dispatch("setUserInfo", o);
        }), e.$api.menu.getMenu().then((n) => {
          const o = n.data;
          te(o), e.$store.dispatch("generateRoutes", o);
          const r = e.$store.getters.addRouters;
          r && r.forEach((c) => {
            e.$router.addRoute(c);
          }), e.$router.push({ path: e.redirect || "/" });
        }), e.$api.config.getObject("/Admin/Sys").then((n) => {
          const o = n.data.value;
          e.$store.dispatch("setSysConfig", o);
        });
      });
    },
    ssoClick(e) {
      location.href = this.baseUrl + e;
    },
    getUrl(e) {
      let t = this, l = `/Sso/Login?name=${e.name}&source=front-end`, a = encodeURIComponent(
        location.origin + "/auth-redirect" + (t.redirect ? "?redirect=" + t.redirect : "")
      );
      return l += `&redirect_uri=${a}`, l;
    },
    getLogoUrl(e) {
      let t = this;
      return e.indexOf("http") !== 0 && (e = t.baseUrl + e), e;
    },
    autoAuthRedirect() {
      let e = this, t = e.loginConfig;
      t && !t.allowLogin && t.providers.length === 1 && e.ssoClick(e.getUrl(t.providers[0]));
    }
  }
});
const st = (e) => (B("data-v-43e88220"), e = e(), z(), e), ns = { class: "center" }, os = { class: "login-col" }, rs = /* @__PURE__ */ st(() => /* @__PURE__ */ d("i", { class: "el-icon-cloudy" }, null, -1)), ss = { class: "heading text-primary" }, is = {
  key: 0,
  class: "center"
}, us = /* @__PURE__ */ st(() => /* @__PURE__ */ d("p", { class: "login3" }, [
  /* @__PURE__ */ d("span", { class: "left" }),
  /* @__PURE__ */ I(" 第三方登录 "),
  /* @__PURE__ */ d("span", { class: "right" })
], -1)), ds = ["title", "onClick"], cs = ["src"];
function ms(e, t, l, a, n, o) {
  const r = m("el-col"), c = m("el-row"), _ = m("el-input"), b = m("el-form-item"), $ = m("el-checkbox"), y = m("el-form");
  return s(), h("div", ns, [
    d("div", os, [
      d("div", null, [
        i(c, null, {
          default: u(() => [
            i(r, {
              span: 24,
              class: "login-logo"
            }, {
              default: u(() => [
                rs
              ]),
              _: 1
            })
          ]),
          _: 1
        }),
        e.loginConfig.allowLogin ? (s(), h(S, { key: 0 }, [
          i(y, {
            model: e.loginForm,
            size: "default",
            class: "cube-login"
          }, {
            default: u(() => [
              d("span", ss, w(e.displayName) + " 登录", 1),
              i(b, { label: "" }, {
                default: u(() => [
                  i(_, {
                    modelValue: e.loginForm.username,
                    "onUpdate:modelValue": t[0] || (t[0] = (p) => e.loginForm.username = p),
                    placeholder: "用户名 / 邮箱",
                    "prefix-icon": "el-icon-user"
                  }, null, 8, ["modelValue"])
                ]),
                _: 1
              }),
              i(b, { label: "" }, {
                default: u(() => [
                  i(_, {
                    modelValue: e.loginForm.password,
                    "onUpdate:modelValue": t[1] || (t[1] = (p) => e.loginForm.password = p),
                    placeholder: "密码",
                    "prefix-icon": "el-icon-lock",
                    "show-password": ""
                  }, null, 8, ["modelValue"])
                ]),
                _: 1
              }),
              i(b, { label: "" }, {
                default: u(() => [
                  i($, {
                    class: "text text-primary",
                    modelValue: e.loginForm.remember,
                    "onUpdate:modelValue": t[2] || (t[2] = (p) => e.loginForm.remember = p)
                  }, {
                    default: u(() => [
                      I(" 记住我 ")
                    ]),
                    _: 1
                  }, 8, ["modelValue"])
                ]),
                _: 1
              })
            ]),
            _: 1
          }, 8, ["model"]),
          d("button", {
            class: "btn",
            onClick: t[3] || (t[3] = (...p) => e.login && e.login(...p))
          }, "登录")
        ], 64)) : T("", !0)
      ]),
      e.loginConfig.providers.length > 0 ? (s(), h("div", is, [
        us,
        i(c, null, {
          default: u(() => [
            i(r, { sm: 24 }, {
              default: u(() => [
                (s(!0), h(S, null, U(e.loginConfig.providers, (p, f) => (s(), h("a", {
                  key: f,
                  title: p.nickName || p.name,
                  onClick: (g) => e.ssoClick(e.getUrl(p))
                }, [
                  p.logo ? (s(), h("img", {
                    key: 0,
                    src: e.getLogoUrl(p.logo),
                    style: { width: "64px", height: "64px" }
                  }, null, 8, cs)) : (s(), h(S, { key: 1 }, [
                    I(w(p.nickName || p.name), 1)
                  ], 64))
                ], 8, ds))), 128))
              ]),
              _: 1
            })
          ]),
          _: 1
        })
      ])) : T("", !0)
    ])
  ]);
}
const ps = /* @__PURE__ */ D(as, [["render", ms], ["__scopeId", "data-v-43e88220"]]), hs = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: ps
}, Symbol.toStringTag, { value: "Module" })), fs = k({
  components: {
    FormControl: ue
  },
  data() {
    return {
      form: {},
      fields: [],
      typeMap: { Add: "新增", Detail: "查看", Edit: "编辑" }
    };
  },
  computed: {
    id() {
      return this.$route.params.id;
    },
    currentPath() {
      const e = this;
      let t = `/${e.type}`;
      return e.isAdd || (t += `/${e.id}`), this.$route.path.replace(t, "");
    },
    type() {
      return this.$route.params.type.toString();
    },
    isAdd() {
      return this.type === "Add";
    },
    isDetail() {
      return this.type === "Detail";
    },
    isEdit() {
      return this.type === "Edit";
    },
    title() {
      return this.typeMap[this.type];
    }
  },
  // watch: {
  // 原本是通过路由变化初始化数据，但是切换页面时仍会触发
  //   $route: {
  //     handler: function() {
  //       this.init()
  //     },
  //     immediate: true
  //   }
  // },
  created() {
    this.init();
  },
  methods: {
    init() {
      this.getColumns(), this.isAdd || this.query();
    },
    getColumns() {
      const e = this, t = e.currentPath;
      e.$api.base.getColumns(t).then((l) => {
        e.fields = l.data;
      });
    },
    query() {
      const e = this;
      e.isDetail ? e.$api.base.getDetailData(e.currentPath, e.id).then((t) => {
        e.form = t.data;
      }) : e.$api.base.getData(e.currentPath, e.id).then((t) => {
        e.form = t.data;
      });
    },
    confirm() {
      const e = this;
      e.isAdd ? e.$api.base.add(e.currentPath, e.form).then(() => {
        e.$message({
          message: "新增成功",
          type: "success",
          duration: 5 * 1e3
        }), e.$router.go(-1);
      }) : e.$api.base.edit(e.currentPath, e.form).then(() => {
        e.$message({
          message: "保存成功",
          type: "success",
          duration: 5 * 1e3
        }), e.$router.go(-1);
      });
    },
    returnIndex() {
      this.$router.push(this.currentPath);
    },
    showInForm(e) {
      const t = this;
      return t.isAdd ? e.showInAddForm : t.isDetail ? e.showInDetailForm : e.showInEditForm;
    }
  }
});
const gs = { style: { display: "inline-flex" } }, _s = {
  key: 1,
  style: { width: "220px", "word-break": "break-all" }
}, bs = { style: { position: "fixed", margin: "20px", float: "right", bottom: "0px", right: "0px", "z-index": "1" } };
function ys(e, t, l, a, n, o) {
  const r = m("InfoFilled"), c = m("el-icon"), _ = m("el-tooltip"), b = m("FormControl"), $ = m("el-form-item"), y = m("el-button"), p = m("el-form");
  return s(), h("div", null, [
    d("div", null, w(e.title), 1),
    i(p, {
      ref: "form",
      modelValue: e.form,
      "onUpdate:modelValue": t[1] || (t[1] = (f) => e.form = f),
      "label-position": "right",
      "label-width": "135px",
      inline: !0,
      class: "form-container"
    }, {
      default: u(() => [
        (s(!0), h(S, null, U(e.fields, (f, g) => (s(), h(S, null, [
          f.name.toLowerCase() != "id" && e.showInForm(f) ? (s(), v($, {
            key: g,
            prop: f.isDataObjectField ? f.name : f.columnName,
            label: (f.displayName || f.name) + "："
          }, Lt({
            default: u(() => [
              e.isDetail ? (s(), h("span", _s, w(e.form[f.name]), 1)) : (s(), v(b, {
                key: 0,
                modelValue: e.form,
                "onUpdate:modelValue": t[0] || (t[0] = (C) => e.form = C),
                configs: f
              }, null, 8, ["modelValue", "configs"]))
            ]),
            _: 2
          }, [
            f.description && f.displayName != f.description ? {
              name: "label",
              fn: u(() => [
                d("div", gs, [
                  d("span", null, w(f.displayName || f.name), 1),
                  i(_, {
                    content: f.description
                  }, {
                    default: u(() => [
                      i(c, null, {
                        default: u(() => [
                          i(r)
                        ]),
                        _: 1
                      })
                    ]),
                    _: 2
                  }, 1032, ["content"])
                ])
              ]),
              key: "0"
            } : void 0
          ]), 1032, ["prop", "label"])) : T("", !0)
        ], 64))), 256)),
        i($, null, {
          default: u(() => [
            d("div", bs, [
              i(y, { onClick: e.returnIndex }, {
                default: u(() => [
                  I("返回")
                ]),
                _: 1
              }, 8, ["onClick"]),
              e.isDetail ? T("", !0) : (s(), v(y, {
                key: 0,
                type: "primary",
                onClick: e.confirm
              }, {
                default: u(() => [
                  I("保存")
                ]),
                _: 1
              }, 8, ["onClick"]))
            ])
          ]),
          _: 1
        })
      ]),
      _: 1
    }, 8, ["modelValue"])
  ]);
}
const vs = /* @__PURE__ */ D(fs, [["render", ys], ["__scopeId", "data-v-8d048d83"]]), ws = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: vs
}, Symbol.toStringTag, { value: "Module" }));
const Is = {
  components: {
    Navbar: le,
    Sidebar: ae,
    AppMain: ne
  },
  mixins: [at],
  computed: {
    sidebar() {
      return this.$store.state.app.sidebar;
    },
    device() {
      return this.$store.state.app.device;
    },
    hiddenLayout() {
      let e = this.$route.query;
      return e.hiddenLayout === "true" || e.hl === "true";
    },
    classObj() {
      return {
        hideSidebar: !this.sidebar.opened,
        openSidebar: this.sidebar.opened,
        withoutAnimation: this.sidebar.withoutAnimation,
        mobile: this.device === "mobile"
      };
    },
    classAppMain() {
      return {
        hiddenLayout: this.hiddenLayout,
        hideSidebarMain: !this.sidebar.opened,
        openSidebarMain: this.sidebar.opened
      };
    }
  },
  methods: {
    handleClickOutside() {
      this.$store.dispatch("closeSideBar", { withoutAnimation: !1 });
    }
  }
};
function Ts(e, t, l, a, n, o) {
  const r = m("navbar"), c = m("sidebar"), _ = m("app-main");
  return s(), h("div", {
    class: P([o.classObj, "app-wrapper"])
  }, [
    d("div", null, [
      A(d("div", {
        class: "drawer-bg",
        onClick: t[0] || (t[0] = (...b) => o.handleClickOutside && o.handleClickOutside(...b))
      }, null, 512), [
        [N, !o.hiddenLayout && o.device === "mobile" && o.sidebar.opened]
      ]),
      A(i(r, null, null, 512), [
        [N, !o.hiddenLayout]
      ]),
      A(i(c, { class: "sidebar sidebar-container" }, null, 512), [
        [N, !o.hiddenLayout]
      ]),
      d("div", {
        class: P([o.classAppMain, "main"])
      }, [
        i(_)
      ], 2)
    ])
  ], 2);
}
const $s = /* @__PURE__ */ D(Is, [["render", Ts], ["__scopeId", "data-v-5fe649e3"]]), Ss = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: $s
}, Symbol.toStringTag, { value: "Module" }));
class oe {
  constructor(t) {
    /** axios实例 */
    ve(this, "request");
    return this.request = t, this;
  }
  /**
   * 获取表对应的列
   * @param {*} path 基础请求路径
   * @returns
   */
  getColumns(t) {
    const l = this.request;
    return l({
      url: t + "/GetColumns",
      method: "get"
    });
  }
  getDataList(t, l) {
    const a = this.request;
    return a({
      url: t + "/Index",
      method: "post",
      data: l
    });
  }
  getData(t, l) {
    const a = this.request, n = {
      id: l
    };
    return a({
      url: t + "/Edit",
      method: "get",
      params: n
    });
  }
  getDetailData(t, l) {
    const a = this.request, n = {
      id: l
    };
    return a({
      url: t + "/Detail",
      method: "get",
      params: n
    });
  }
  deleteById(t, l) {
    const a = this.request, n = {
      id: l
    };
    return a({
      url: t + "/Delete",
      method: "get",
      params: n
    });
  }
  add(t, l) {
    const a = this.request;
    return a({
      url: t + "/Add",
      method: "post",
      data: l
    });
  }
  edit(t, l) {
    const a = this.request;
    return a({
      url: t + "/Edit",
      method: "post",
      data: l
    });
  }
}
class Cs extends oe {
  getObject(t) {
    const l = this.request;
    return l({
      url: t + "/Index",
      method: "get"
    });
  }
  updateObject(t, l) {
    const a = this.request;
    return a({
      url: t + "/Update",
      method: "post",
      data: l
    });
  }
  getLoginConfig() {
    const t = this.request;
    return t({
      url: "/Admin/Cube/GetLoginConfig",
      method: "get"
    });
  }
}
class Fs extends oe {
  getMenu() {
    const t = this.request;
    return t({
      url: "/Admin/Index/GetMenuTree",
      method: "get"
    });
  }
}
const fe = "token";
function ge() {
  return E.getItem(fe);
}
function Ds(e) {
  return E.setItem(fe, e);
}
function it() {
  return E.removeItem(fe);
}
const _e = "userInfo";
function ks() {
  const e = E.getItem(_e);
  return e && e !== "undefined" ? JSON.parse(e) : null;
}
function Us(e) {
  return E.setItem(_e, JSON.stringify(e));
}
function ut() {
  return E.removeItem(_e);
}
class Os extends oe {
  login(t) {
    const l = this.request;
    return l({
      url: "/Admin/User/Login",
      method: "post",
      data: t
      // params: data,
    });
  }
  /**
   * 注销登陆之后，移除token、用户信息、菜单
   * @returns
   */
  logout() {
    const t = this.request;
    return t({
      url: "/Admin/User/Logout",
      method: "get"
    }).then(() => {
      it(), ut(), et();
    });
  }
  getUserInfo() {
    const t = this.request;
    return t({
      url: "/Admin/User/Info/",
      method: "get"
    });
  }
  updateUserInfo(t) {
    const l = this.request;
    return l({
      url: "/Admin/User/Info/",
      method: "post",
      data: t
    });
  }
  changePassword(t) {
    const l = this.request;
    return l({
      url: "/Admin/User/ChangePassword",
      method: "post",
      data: t
    });
  }
}
let K;
const Es = (e, t) => (K = {
  base: new oe(t),
  user: new Os(t),
  menu: new Fs(t),
  config: new Cs(t)
}, e.config.globalProperties.$api = K, K);
function As() {
  if (!K)
    throw new Error("请调用createApi方法创建api");
  return K;
}
let Z = !1;
function Ie() {
  if (!Z)
    Z = !0;
  else {
    console.log("登录超时，已弹窗，尝试关闭已打开的弹窗");
    try {
      we.close();
    } catch {
    }
  }
  return we.confirm("登陆超时，可以取消继续留在该页面，或者重新登录", "确定登出", {
    confirmButtonText: "重新登录",
    cancelButtonText: "取消",
    type: "warning"
  }).then(() => {
    Z = !1, As().user.logout().then(() => {
      Z = !1, location.reload();
    });
  }), Promise.reject("登录超时");
}
function Te(e) {
  return x({
    message: e.message,
    type: "warning",
    duration: 5 * 1e3
  }), Promise.reject("没有权限");
}
const $e = {
  timeout: 5e4
  // // 响应拦截处理大数值
  // transformResponse: [
  //   (data) => {
  //     try {
  //       // 使用正则将长整数替换为字符串
  //       data = data.replace(/(\d{16,})/gi, '"$1"')
  //       return JSON.parse(data)
  //       // // 使用json-bigint将大数值转成
  //       // data = JSONbig.parse(data)
  //       // return data
  //     } catch (err) {
  //       return data
  //     }
  //   }
  // ]
}, Se = {
  request: (e) => {
    e.headers["Content-Type"] = "application/json; charset=UTF-8";
    const t = ge();
    return t && (e.headers.Authorization = t), e;
  },
  requestError: (e) => (console.log(e), x({
    message: e,
    type: "error",
    duration: 5 * 1e3
  }), Promise.reject(e)),
  response: (e) => {
    let t = e.data;
    return typeof t == "string" && (t = JSON.parse(t)), t.code !== void 0 && t.code !== null ? t.code >= 500 ? (x({
      message: t.message,
      type: "error",
      duration: 5 * 1e3
    }), Promise.reject("后端服务错误")) : t.code === 401 ? Ie() : t.code === 403 ? Te(t) : t : (console.log("格式错误", t), x({
      message: "服务端返回格式不正确!!!请联系管理员",
      type: "error",
      duration: 5 * 1e3
    }), Promise.reject("服务端返回格式不正确"));
  },
  responseError: (e) => (e.message === "Request failed with status code 401" ? Ie() : e.message === "Request failed with status code 403" ? Te({ message: "没有权限！" }) : (console.log("err", e, JSON.stringify(e)), x({
    message: "服务请求出错",
    type: "error",
    duration: 5 * 1e3
  })), Promise.reject(e))
}, Ps = (e, t = void 0, l = void 0) => {
  t && t($e), l && l(Se);
  const a = e.config.globalProperties.$http = zt.create(
    $e
  ), n = Se;
  return a.interceptors.request.use(n.request, n.requestError), a.interceptors.response.use(n.response, n.responseError), a;
};
var Vs = typeof Y == "object" && Y && Y.Object === Object && Y, Ns = Vs, js = Ns, Ls = typeof self == "object" && self && self.Object === Object && self, Ms = js || Ls || Function("return this")(), Rs = Ms, Bs = Rs, zs = Bs.Symbol, be = zs;
function qs(e, t) {
  for (var l = -1, a = e == null ? 0 : e.length, n = Array(a); ++l < a; )
    n[l] = t(e[l], l, e);
  return n;
}
var Hs = qs, xs = Array.isArray, Ks = xs, Ce = be, dt = Object.prototype, Ws = dt.hasOwnProperty, Gs = dt.toString, H = Ce ? Ce.toStringTag : void 0;
function Ys(e) {
  var t = Ws.call(e, H), l = e[H];
  try {
    e[H] = void 0;
    var a = !0;
  } catch {
  }
  var n = Gs.call(e);
  return a && (t ? e[H] = l : delete e[H]), n;
}
var Js = Ys, Zs = Object.prototype, Qs = Zs.toString;
function Xs(e) {
  return Qs.call(e);
}
var ei = Xs, Fe = be, ti = Js, li = ei, ai = "[object Null]", ni = "[object Undefined]", De = Fe ? Fe.toStringTag : void 0;
function oi(e) {
  return e == null ? e === void 0 ? ni : ai : De && De in Object(e) ? ti(e) : li(e);
}
var ri = oi;
function si(e) {
  return e != null && typeof e == "object";
}
var ii = si, ui = ri, di = ii, ci = "[object Symbol]";
function mi(e) {
  return typeof e == "symbol" || di(e) && ui(e) == ci;
}
var pi = mi, ke = be, hi = Hs, fi = Ks, gi = pi, _i = 1 / 0, Ue = ke ? ke.prototype : void 0, Oe = Ue ? Ue.toString : void 0;
function ct(e) {
  if (typeof e == "string")
    return e;
  if (fi(e))
    return hi(e, ct) + "";
  if (gi(e))
    return Oe ? Oe.call(e) : "";
  var t = e + "";
  return t == "0" && 1 / e == -_i ? "-0" : t;
}
var bi = ct, yi = bi;
function vi(e) {
  return e == null ? "" : yi(e);
}
var re = vi;
function wi(e, t, l) {
  var a = -1, n = e.length;
  t < 0 && (t = -t > n ? 0 : n + t), l = l > n ? n : l, l < 0 && (l += n), n = t > l ? 0 : l - t >>> 0, t >>>= 0;
  for (var o = Array(n); ++a < n; )
    o[a] = e[a + t];
  return o;
}
var Ii = wi, Ti = Ii;
function $i(e, t, l) {
  var a = e.length;
  return l = l === void 0 ? a : l, !t && l >= a ? e : Ti(e, t, l);
}
var Si = $i, Ci = "\\ud800-\\udfff", Fi = "\\u0300-\\u036f", Di = "\\ufe20-\\ufe2f", ki = "\\u20d0-\\u20ff", Ui = Fi + Di + ki, Oi = "\\ufe0e\\ufe0f", Ei = "\\u200d", Ai = RegExp("[" + Ei + Ci + Ui + Oi + "]");
function Pi(e) {
  return Ai.test(e);
}
var mt = Pi;
function Vi(e) {
  return e.split("");
}
var Ni = Vi, pt = "\\ud800-\\udfff", ji = "\\u0300-\\u036f", Li = "\\ufe20-\\ufe2f", Mi = "\\u20d0-\\u20ff", Ri = ji + Li + Mi, Bi = "\\ufe0e\\ufe0f", zi = "[" + pt + "]", se = "[" + Ri + "]", ie = "\\ud83c[\\udffb-\\udfff]", qi = "(?:" + se + "|" + ie + ")", ht = "[^" + pt + "]", ft = "(?:\\ud83c[\\udde6-\\uddff]){2}", gt = "[\\ud800-\\udbff][\\udc00-\\udfff]", Hi = "\\u200d", _t = qi + "?", bt = "[" + Bi + "]?", xi = "(?:" + Hi + "(?:" + [ht, ft, gt].join("|") + ")" + bt + _t + ")*", Ki = bt + _t + xi, Wi = "(?:" + [ht + se + "?", se, ft, gt, zi].join("|") + ")", Gi = RegExp(ie + "(?=" + ie + ")|" + Wi + Ki, "g");
function Yi(e) {
  return e.match(Gi) || [];
}
var Ji = Yi, Zi = Ni, Qi = mt, Xi = Ji;
function eu(e) {
  return Qi(e) ? Xi(e) : Zi(e);
}
var tu = eu, lu = Si, au = mt, nu = tu, ou = re;
function ru(e) {
  return function(t) {
    t = ou(t);
    var l = au(t) ? nu(t) : void 0, a = l ? l[0] : t.charAt(0), n = l ? lu(l, 1).join("") : t.slice(1);
    return a[e]() + n;
  };
}
var su = ru, iu = su, uu = iu("toUpperCase"), yt = uu, du = re, cu = yt;
function mu(e) {
  return cu(du(e).toLowerCase());
}
var pu = mu;
function hu(e, t, l, a) {
  var n = -1, o = e == null ? 0 : e.length;
  for (a && o && (l = e[++n]); ++n < o; )
    l = t(l, e[n], n, e);
  return l;
}
var fu = hu;
function gu(e) {
  return function(t) {
    return e == null ? void 0 : e[t];
  };
}
var _u = gu, bu = _u, yu = {
  // Latin-1 Supplement block.
  À: "A",
  Á: "A",
  Â: "A",
  Ã: "A",
  Ä: "A",
  Å: "A",
  à: "a",
  á: "a",
  â: "a",
  ã: "a",
  ä: "a",
  å: "a",
  Ç: "C",
  ç: "c",
  Ð: "D",
  ð: "d",
  È: "E",
  É: "E",
  Ê: "E",
  Ë: "E",
  è: "e",
  é: "e",
  ê: "e",
  ë: "e",
  Ì: "I",
  Í: "I",
  Î: "I",
  Ï: "I",
  ì: "i",
  í: "i",
  î: "i",
  ï: "i",
  Ñ: "N",
  ñ: "n",
  Ò: "O",
  Ó: "O",
  Ô: "O",
  Õ: "O",
  Ö: "O",
  Ø: "O",
  ò: "o",
  ó: "o",
  ô: "o",
  õ: "o",
  ö: "o",
  ø: "o",
  Ù: "U",
  Ú: "U",
  Û: "U",
  Ü: "U",
  ù: "u",
  ú: "u",
  û: "u",
  ü: "u",
  Ý: "Y",
  ý: "y",
  ÿ: "y",
  Æ: "Ae",
  æ: "ae",
  Þ: "Th",
  þ: "th",
  ß: "ss",
  // Latin Extended-A block.
  Ā: "A",
  Ă: "A",
  Ą: "A",
  ā: "a",
  ă: "a",
  ą: "a",
  Ć: "C",
  Ĉ: "C",
  Ċ: "C",
  Č: "C",
  ć: "c",
  ĉ: "c",
  ċ: "c",
  č: "c",
  Ď: "D",
  Đ: "D",
  ď: "d",
  đ: "d",
  Ē: "E",
  Ĕ: "E",
  Ė: "E",
  Ę: "E",
  Ě: "E",
  ē: "e",
  ĕ: "e",
  ė: "e",
  ę: "e",
  ě: "e",
  Ĝ: "G",
  Ğ: "G",
  Ġ: "G",
  Ģ: "G",
  ĝ: "g",
  ğ: "g",
  ġ: "g",
  ģ: "g",
  Ĥ: "H",
  Ħ: "H",
  ĥ: "h",
  ħ: "h",
  Ĩ: "I",
  Ī: "I",
  Ĭ: "I",
  Į: "I",
  İ: "I",
  ĩ: "i",
  ī: "i",
  ĭ: "i",
  į: "i",
  ı: "i",
  Ĵ: "J",
  ĵ: "j",
  Ķ: "K",
  ķ: "k",
  ĸ: "k",
  Ĺ: "L",
  Ļ: "L",
  Ľ: "L",
  Ŀ: "L",
  Ł: "L",
  ĺ: "l",
  ļ: "l",
  ľ: "l",
  ŀ: "l",
  ł: "l",
  Ń: "N",
  Ņ: "N",
  Ň: "N",
  Ŋ: "N",
  ń: "n",
  ņ: "n",
  ň: "n",
  ŋ: "n",
  Ō: "O",
  Ŏ: "O",
  Ő: "O",
  ō: "o",
  ŏ: "o",
  ő: "o",
  Ŕ: "R",
  Ŗ: "R",
  Ř: "R",
  ŕ: "r",
  ŗ: "r",
  ř: "r",
  Ś: "S",
  Ŝ: "S",
  Ş: "S",
  Š: "S",
  ś: "s",
  ŝ: "s",
  ş: "s",
  š: "s",
  Ţ: "T",
  Ť: "T",
  Ŧ: "T",
  ţ: "t",
  ť: "t",
  ŧ: "t",
  Ũ: "U",
  Ū: "U",
  Ŭ: "U",
  Ů: "U",
  Ű: "U",
  Ų: "U",
  ũ: "u",
  ū: "u",
  ŭ: "u",
  ů: "u",
  ű: "u",
  ų: "u",
  Ŵ: "W",
  ŵ: "w",
  Ŷ: "Y",
  ŷ: "y",
  Ÿ: "Y",
  Ź: "Z",
  Ż: "Z",
  Ž: "Z",
  ź: "z",
  ż: "z",
  ž: "z",
  Ĳ: "IJ",
  ĳ: "ij",
  Œ: "Oe",
  œ: "oe",
  ŉ: "'n",
  ſ: "s"
}, vu = bu(yu), wu = vu, Iu = wu, Tu = re, $u = /[\xc0-\xd6\xd8-\xf6\xf8-\xff\u0100-\u017f]/g, Su = "\\u0300-\\u036f", Cu = "\\ufe20-\\ufe2f", Fu = "\\u20d0-\\u20ff", Du = Su + Cu + Fu, ku = "[" + Du + "]", Uu = RegExp(ku, "g");
function Ou(e) {
  return e = Tu(e), e && e.replace($u, Iu).replace(Uu, "");
}
var Eu = Ou, Au = /[^\x00-\x2f\x3a-\x40\x5b-\x60\x7b-\x7f]+/g;
function Pu(e) {
  return e.match(Au) || [];
}
var Vu = Pu, Nu = /[a-z][A-Z]|[A-Z]{2}[a-z]|[0-9][a-zA-Z]|[a-zA-Z][0-9]|[^a-zA-Z0-9 ]/;
function ju(e) {
  return Nu.test(e);
}
var Lu = ju, vt = "\\ud800-\\udfff", Mu = "\\u0300-\\u036f", Ru = "\\ufe20-\\ufe2f", Bu = "\\u20d0-\\u20ff", zu = Mu + Ru + Bu, wt = "\\u2700-\\u27bf", It = "a-z\\xdf-\\xf6\\xf8-\\xff", qu = "\\xac\\xb1\\xd7\\xf7", Hu = "\\x00-\\x2f\\x3a-\\x40\\x5b-\\x60\\x7b-\\xbf", xu = "\\u2000-\\u206f", Ku = " \\t\\x0b\\f\\xa0\\ufeff\\n\\r\\u2028\\u2029\\u1680\\u180e\\u2000\\u2001\\u2002\\u2003\\u2004\\u2005\\u2006\\u2007\\u2008\\u2009\\u200a\\u202f\\u205f\\u3000", Tt = "A-Z\\xc0-\\xd6\\xd8-\\xde", Wu = "\\ufe0e\\ufe0f", $t = qu + Hu + xu + Ku, St = "['’]", Ee = "[" + $t + "]", Gu = "[" + zu + "]", Ct = "\\d+", Yu = "[" + wt + "]", Ft = "[" + It + "]", Dt = "[^" + vt + $t + Ct + wt + It + Tt + "]", Ju = "\\ud83c[\\udffb-\\udfff]", Zu = "(?:" + Gu + "|" + Ju + ")", Qu = "[^" + vt + "]", kt = "(?:\\ud83c[\\udde6-\\uddff]){2}", Ut = "[\\ud800-\\udbff][\\udc00-\\udfff]", R = "[" + Tt + "]", Xu = "\\u200d", Ae = "(?:" + Ft + "|" + Dt + ")", ed = "(?:" + R + "|" + Dt + ")", Pe = "(?:" + St + "(?:d|ll|m|re|s|t|ve))?", Ve = "(?:" + St + "(?:D|LL|M|RE|S|T|VE))?", Ot = Zu + "?", Et = "[" + Wu + "]?", td = "(?:" + Xu + "(?:" + [Qu, kt, Ut].join("|") + ")" + Et + Ot + ")*", ld = "\\d*(?:1st|2nd|3rd|(?![123])\\dth)(?=\\b|[A-Z_])", ad = "\\d*(?:1ST|2ND|3RD|(?![123])\\dTH)(?=\\b|[a-z_])", nd = Et + Ot + td, od = "(?:" + [Yu, kt, Ut].join("|") + ")" + nd, rd = RegExp([
  R + "?" + Ft + "+" + Pe + "(?=" + [Ee, R, "$"].join("|") + ")",
  ed + "+" + Ve + "(?=" + [Ee, R + Ae, "$"].join("|") + ")",
  R + "?" + Ae + "+" + Pe,
  R + "+" + Ve,
  ad,
  ld,
  Ct,
  od
].join("|"), "g");
function sd(e) {
  return e.match(rd) || [];
}
var id = sd, ud = Vu, dd = Lu, cd = re, md = id;
function pd(e, t, l) {
  return e = cd(e), t = l ? void 0 : t, t === void 0 ? dd(e) ? md(e) : ud(e) : e.match(t) || [];
}
var hd = pd, fd = fu, gd = Eu, _d = hd, bd = "['’]", yd = RegExp(bd, "g");
function vd(e) {
  return function(t) {
    return fd(_d(gd(t).replace(yd, "")), e, "");
  };
}
var wd = vd, Id = pu, Td = wd, $d = Td(function(e, t, l) {
  return t = t.toLowerCase(), e + (l ? Id(t) : t);
}), Sd = $d;
const Cd = (e, t) => {
  Object.entries(t).forEach(([l, a]) => {
    var r;
    const n = a, o = yt(
      Sd(
        // 获取目录深度无关的文件名
        (r = l.split("/").pop()) == null ? void 0 : r.replace(/\.\w+$/, "")
      )
    );
    e.component(
      o,
      // 在 `.default` 上查找组件选项。
      // 如果组件导出了 `export default` 的话，该选项会存在。
      // 否则回退到模块的根。
      n.default || n
    );
  });
};
let X = {};
function G(e) {
  const t = Pt(e);
  if (!t)
    throw new Error("找不到模块：" + e);
  return X[t];
}
const At = function() {
  return Object.keys(X);
}, Fd = function(t) {
  X = { ...X, ...t };
}, Pt = (e) => (e.startsWith("@/") && (e = e.replace("@/", "/src/")), At().includes(e) ? e : null);
G.addFiles = Fd;
G.id = "fileContext";
G.keys = At;
G.resolve = Pt;
const L = G, Dd = ["/login", "/auth-redirect"], kd = (e, t, l) => {
  ge() ? e.path === "/login" ? l({
    path: "/"
  }) : l() : Dd.indexOf(e.path) !== -1 ? l() : l(`/login?redirect=${e.path}`);
}, Ne = () => Promise.resolve(L("@/views/layout/index.vue")), Ud = [
  // {
  //   path: '/redirect',
  //   component: Layout,
  //   hidden: true,
  //   children: [
  //     {
  //       path: '/redirect/:path*',
  //       component: () => import('src/views/redirect'),
  //     },
  //   ],
  // },
  {
    path: "/login",
    component: () => Promise.resolve(L("@/views/account/login.vue")),
    hidden: !0
  },
  {
    path: "/auth-redirect",
    component: () => Promise.resolve(L("@/views/account/auth-redirect.vue")),
    hidden: !0
  },
  {
    path: "",
    redirect: "/Admin/User/Info",
    component: Ne,
    children: [
      {
        path: "/Admin/User/Info",
        component: () => Promise.resolve(L("@/views/Admin/User/info.vue")),
        name: "UserInfo",
        meta: {
          title: "个人信息",
          noCache: !0
        }
      }
    ]
  },
  // {
  //   path: '/404',
  //   component: () => import('src/views/errorPage/404'),
  //   hidden: true,
  // },
  // {
  //   path: '/401',
  //   component: () => import('src/views/errorPage/401'),
  //   hidden: true,
  // },
  {
    path: "",
    component: Ne,
    // redirect: 'dashboard',
    children: [
      {
        path: "dashboard",
        component: () => Promise.resolve(L("@/views/Admin/Index/Main.vue")),
        name: "Dashboard",
        meta: {
          title: "首页",
          icon: "dashboard",
          noCache: !0
        }
      }
    ]
  },
  {
    path: "/Admin/Index/Main",
    redirect: "dashboard"
  }
], je = {
  history: Ht(),
  scrollBehavior: () => ({
    top: 0
  }),
  routes: Ud
}, Od = (e, t = null, l = null, a = null) => {
  t && t(je);
  const n = xt(je);
  return e.use(n), l ? n.beforeEach(l) : n.beforeEach(kd), a && n.afterEach(a), n;
};
function Vt(e, t, l = 0) {
  return t.forEach((a) => {
    if (a.path = a.url, a.path.startsWith("~") && (a.path = a.path.substr(1)), a.displayName = a.displayName || a.name, a.visible === void 0 && console.log(a.name + " visible为空"), a.meta ? (a.meta.menuId = a.id, a.meta.permissions = a.permissions) : a.meta = { menuId: a.id, permissions: a.permissions }, l === 0)
      a.component = () => Promise.resolve(e("@/views/layout/index.vue"));
    else {
      a.name = a.path.replace(/\//g, "");
      let o = `@/views${a.path}/list.vue`;
      a.component = () => {
        let r = {};
        const c = `@/views${a.path}/config.tsx`;
        return e.resolve(c) && (r = e(c)), e.resolve(o) || (o = "@/views/common/list.vue"), Promise.resolve(Mt(e(o).default, { ...r }));
      }, t.push(Ed(e, a, a.path));
    }
    let n = a.children;
    n && n instanceof Array ? (a.hasChildren = !0, n = Vt(e, n, l + 1)) : a.hasChildren = !1, a.children = n;
  }), t;
}
function Ed(e, t, l) {
  return {
    visible: !1,
    path: `${l}/:type(Edit|Add|Detail)/:id?`,
    // path: `User/Edit/:id?`,
    name: t.name + "Form",
    isFormRoute: !0,
    // 是否表单路由
    component: () => {
      const n = `@/views${l}/form.vue`;
      return e.resolve(n) ? Promise.resolve(e(n)) : Promise.resolve(e("@/views/common/form.vue"));
    }
  };
}
const Ad = {
  state: {
    // 将展示在侧边栏的菜单
    // menuRouters: [], // constantRouterMap,
    menuRouters: [],
    // 将要添加到路由系统中的新路由
    addRouters: [],
    // src/views 文件夹下的文件组件
    files(e) {
      return console.log("no module"), null;
    }
  },
  mutations: {
    SET_ROUTERS: (e, t) => {
      e.addRouters = t, e.menuRouters = /* constantRouterMap.concat*/
      t;
    },
    ADD_ROUTERS: (e, t) => {
      e.addRouters = e.addRouters.concat(t);
    },
    SET_FILES: (e, t) => {
      let l = e.files.map || {};
      l = { ...l, ...t }, e.files = (a) => l[a], e.files.map = l, e.files.keys = () => Object.keys(l);
    }
  },
  actions: {
    generateRoutes({ commit: e, state: t }, l) {
      const a = Vt(L, l);
      e("SET_ROUTERS", a);
    },
    setRouters({ commit: e }, t) {
      e("SET_ROUTERS", t);
    },
    setFiles({ commit: e }, t) {
      e("SET_FILES", t);
    }
  }
}, Pd = {
  state: {
    userInfo: ks(),
    permission: void 0,
    // 权限集合
    token: ge(),
    hasPermission: Vd
  },
  mutations: {
    SET_USERINFO: (e, t) => {
      Us(t), e.userInfo = t;
    },
    REMOVE_USERINFO: (e) => {
      ut(), e.userInfo = void 0;
    },
    SET_TOKEN: (e, t) => {
      Ds(t), e.token = t;
    },
    REMOVE_TOKEN: (e) => {
      it(), e.token = void 0;
    },
    SET_PERMISSION: (e, t) => {
      e.permission = t;
    },
    REMOVE_MENU: (e) => {
      et();
    }
  },
  actions: {
    setToken({ commit: e }, t) {
      e("SET_TOKEN", t);
    },
    // 设置用户信息
    setUserInfo({ commit: e }, t) {
      e("SET_USERINFO", t);
    },
    // 登出
    logout({ commit: e, state: t }) {
      e("REMOVE_TOKEN"), e("REMOVE_USERINFO"), e("REMOVE_MENU");
    }
  }
};
function Vd(e, { menuId: t, actionId: l, permissions: a }) {
  const n = e.state.user;
  if (!n.permission) {
    if (!n.userInfo || !n.userInfo.permission)
      return !1;
    const r = n.userInfo.permission, c = {}, _ = r.split(",");
    for (const b in _) {
      const y = _[b].split("#");
      c[y[0]] = y[1];
    }
    e.commit("SET_PERMISSION", c);
  }
  const o = n.permission[t];
  return o === void 0 ? !1 : l == null || l < 1 ? !0 : a[l] ? (o & l) > 0 : !1;
}
const Nd = {
  state: {
    listFields: {},
    addFormFields: {},
    editFormFields: {},
    detailFields: {}
  },
  mutations: {
    SET_ListFields: (e, { key: t, fields: l }) => {
      e.listFields[t] = l;
    },
    SET_AddFormFields: (e, { key: t, fields: l }) => {
      e.addFormFields[t] = l;
    },
    SET_EditFormFields: (e, { key: t, fields: l }) => {
      e.editFormFields[t] = l;
    },
    SET_DetailFields: (e, { key: t, fields: l }) => {
      e.detailFields[t] = l;
    }
  },
  actions: {
    setListFields({ commit: e }, { key: t, fields: l }) {
      e("SET_ListFields", { key: t, fields: l });
    },
    setAddFormFields({ commit: e }, { key: t, fields: l }) {
      e("SET_AddFormFields", { key: t, fields: l });
    },
    setEditFormFields({ commit: e }, { key: t, fields: l }) {
      e("SET_EditFormFields", { key: t, fields: l });
    },
    setDetailFields({ commit: e }, { key: t, fields: l }) {
      e("SET_DetailFields", { key: t, fields: l });
    }
  }
}, jd = {
  baseUrl: "",
  getBaseUrl() {
    return this.baseUrl;
  },
  ssoUrl: "https://sso.newlifex.com",
  login: "/Admin/User/Login",
  getToken: "/Sso/LoginInfo",
  getUserInfo: "/Admin/User/Info/",
  logout: "/Admin/User/Logout",
  changePassword: "/Admin/User/ChangePassword",
  getMenu: "/Admin/Index/GetMenuTree",
  getEntityFields: "/GetFields",
  getColumns: "/GetColumns",
  getDataList: "/Index",
  getData: "/Edit",
  getDetailData: "/Detail",
  deleteById: "/Delete",
  add: "/Add",
  edit: "/Edit",
  getObject: "/Index",
  getSysConfig: "/Admin/Sys",
  updateObject: "/Update",
  getLoginConfig: "/Admin/Cube/GetLoginConfig"
}, Le = E.getItem("loginConfig"), Ld = {
  state: {
    sidebar: {
      opened: !0,
      // !+Storage.getItem('sidebarStatus'),
      withoutAnimation: !1
    },
    device: "desktop",
    size: E.getItem("size") || "default",
    urls: jd,
    // 系统配置
    sysConfig: void 0,
    // 登录页面配置
    loginConfig: Le ? JSON.parse(Le) : null,
    // 是否隐藏布局
    hiddenLayout: !1,
    // 信息弹窗
    message: void 0,
    // 确认框弹窗
    messageBox: void 0
  },
  mutations: {
    TOGGLE_SIDEBAR: (e) => {
      e.sidebar.opened ? E.setItem("sidebarStatus", 1) : E.setItem("sidebarStatus", 0), e.sidebar.opened = !e.sidebar.opened, e.sidebar.withoutAnimation = !1;
    },
    CLOSE_SIDEBAR: (e, t) => {
      E.setItem("sidebarStatus", 1), e.sidebar.opened = !1, e.sidebar.withoutAnimation = t;
    },
    TOGGLE_DEVICE: (e, t) => {
      e.device = t;
    },
    SET_SIZE: (e, t) => {
      e.size = t, E.setItem("size", t);
    },
    SET_URLS: (e, t) => {
      Object.assign(e.urls, t);
    },
    SET_SYSCONFIG: (e, t) => {
      e.sysConfig = t;
    },
    SET_LOGINCONFIG: (e, t) => {
      e.loginConfig = t, E.setItem("loginConfig", JSON.stringify(t));
    },
    SET_HIDDENLAYOUT: (e, t) => {
      e.hiddenLayout = t;
    },
    SET_MESSAGE: (e, t) => {
      e.message = t;
    },
    SET_MESSAGEBOX: (e, t) => {
      e.messageBox = t;
    }
  },
  actions: {
    toggleSideBar({ commit: e }) {
      e("TOGGLE_SIDEBAR");
    },
    closeSideBar({ commit: e }, { withoutAnimation: t }) {
      e("CLOSE_SIDEBAR", t);
    },
    toggleDevice({ commit: e }, t) {
      e("TOGGLE_DEVICE", t);
    },
    setSize({ commit: e }, t) {
      e("SET_SIZE", t);
    },
    setUrls({ commit: e }, t) {
      e("SET_URLS", t);
    },
    setHiddenLayout({ commit: e }, t) {
      e("SET_HIDDENLAYOUT", t);
    },
    setSysConfig({ commit: e }, t) {
      e("SET_SYSCONFIG", t);
    },
    setLoginConfig({ commit: e }, t) {
      e("SET_LOGINCONFIG", t);
    },
    setMessage({ commit: e }, t) {
      e("SET_MESSAGE", t);
    },
    setMessageBox({ commit: e }, t) {
      e("SET_MESSAGEBOX", t);
    }
  }
}, Md = {
  token: (e) => e.user.token,
  userInfo: (e) => e.user.userInfo,
  menuRouters: (e) => e.route.menuRouters,
  addRouters: (e) => e.route.addRouters,
  files: (e) => e.route.files,
  sysConfig: (e) => e.app.sysConfig,
  loginConfig: (e) => e.app.loginConfig,
  sidebar: (e) => e.app.sidebar,
  app: (e) => e.app,
  urls: (e) => e.app.urls,
  message: (e) => e.app.message,
  messageBox: (e) => e.app.messageBox
}, Me = {
  state: {},
  mutations: {},
  actions: {},
  modules: {
    app: Ld,
    entity: Nd,
    route: Ad,
    user: Pd
  },
  getters: Md
}, Rd = (e, t = null) => {
  t && t(Me);
  const l = Kt(Me);
  return e.use(l), l;
}, Bd = /* @__PURE__ */ Object.assign({
  "/src/views/components/FormControl.vue": ze,
  "/src/views/components/NormalTable.vue": qe,
  "/src/views/components/TableOperator.vue": He,
  "/src/views/components/TableSearch.vue": xe
});
let j, Q;
const ee = (e) => {
  if (ee.installed)
    return;
  ee.installed = !0;
  const t = /* @__PURE__ */ Object.assign({ "/src/App.vue": wl, "/src/components/AdvancedTable.vue": Kl, "/src/components/FormControl.vue": Ll, "/src/components/NormalTable.vue": Fl, "/src/components/TableHandler.vue": Vl, "/src/components/TablePagination.vue": xl, "/src/components/TableSearch.vue": Bl, "/src/components/multipleSelect.vue": Jl, "/src/components/singleSelect.vue": ea, "/src/layouts/components/appMain.vue": oa, "/src/layouts/components/hamburger.vue": pa, "/src/layouts/components/navbar.vue": ja, "/src/layouts/components/sidebar/Item.vue": Ba, "/src/layouts/components/sidebar/SidebarItem.vue": xa, "/src/layouts/components/sidebar/index.vue": Xa, "/src/layouts/index.vue": Mn, "/src/pages/auth-redirect.vue": Bn, "/src/pages/docs.vue": Wn, "/src/pages/index.vue": to, "/src/pages/login.vue": ho, "/src/pages/test.vue": vo, "/src/views/Admin/Cube/list.vue": Ao, "/src/views/Admin/Index/Main.vue": wr, "/src/views/Admin/Menu/form.vue": Cr, "/src/views/Admin/Menu/list.vue": Er, "/src/views/Admin/Role/config.tsx": Vr, "/src/views/Admin/Role/form.vue": Rr, "/src/views/Admin/User/config.tsx": xr, "/src/views/Admin/User/info.vue": es, "/src/views/account/auth-redirect.vue": ls, "/src/views/account/login.vue": hs, "/src/views/common/form.vue": ws, "/src/views/common/list.vue": ko, "/src/views/common/objectForm.vue": So, "/src/views/components/FormControl.vue": ze, "/src/views/components/NormalTable.vue": qe, "/src/views/components/TableOperator.vue": He, "/src/views/components/TableSearch.vue": xe, "/src/views/layout/components/appMain.vue": En, "/src/views/layout/components/hamburger.vue": va, "/src/views/layout/components/navbar.vue": gn, "/src/views/layout/components/sidebar/Item.vue": vn, "/src/views/layout/components/sidebar/SidebarItem.vue": $n, "/src/views/layout/components/sidebar/index.vue": Dn, "/src/views/layout/index.vue": Ss });
  L.addFiles(t), e.component("Navbar", le), e.component("Sidebar", ae), e.component("AppMain", ne);
  const l = Rd(e);
  l.dispatch("setFiles", t);
  const a = Ps(e, void 0, (r) => {
  });
  a.interceptors.request.use((r) => (r.baseURL = l.getters.urls.getBaseUrl(), r)), Es(e, a), l.dispatch("setMessage", j.ElMessage), l.dispatch("setMessageBox", j.ElMessageBox);
  const n = he();
  let o = [];
  n && n.length > 0 && (l.dispatch("generateRoutes", n), o = l.getters.addRouters), Od(e, (r) => {
    r.routes = o.concat(r.routes);
  }), e.use(j, { size: l.state.app.size });
  for (const r in Q)
    if (Object.prototype.hasOwnProperty.call(Q, r)) {
      const c = Q[r];
      e.component(c.name, c);
    }
  Cd(e, Bd), e.config.globalProperties.$message = j.ElMessage, e.config.globalProperties.$messageBox = j.ElMessageBox, e.config.globalProperties.$warn = (r) => {
    j.ElMessage.warning(r);
  }, e.config.unwrapInjectedRef = !0;
}, Gd = () => (j = Rt, Q = qt, {
  install: ee
}), Yd = {
  version: "1.0",
  install: ee
};
export {
  ne as AppMain,
  le as Navbar,
  nl as NormalTable,
  ae as Sidebar,
  ml as TableOperator,
  fl as TableSearch,
  Gd as createCubeUI,
  Yd as default,
  L as fileContext
};
//# sourceMappingURL=CubeUI.js.map
