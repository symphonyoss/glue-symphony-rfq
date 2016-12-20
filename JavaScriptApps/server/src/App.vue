<template>
  <div class="container">
    <div class="card-panel blue darken-4">
      <div class="row">
        <div class="col s6">
          <div class="input-field">
            <input type="email" v-model="counterParty" @change="subscribeForRfq" class="white-text" id="counterParty">
            <label for="counterParty" class="white-text">Counter Party</label>
          </div>
          <div class="row">
            <div class="input-field col s6">
              <input type="text" class="white-text" placeholder="Product" v-model="market.symbol" :disabled="market.active">
            </div>
            <div class="input-field col s6">
              <input type="number" min="0" class="white-text" id="spread" placeholder="Spread" v-model="market.spread">
            </div>
          </div>
          <div class="input-field col s6">
            <input type="checkbox" class="white-text" id="autoquote" v-model="market.autoquote">
            <label for="autoquote" class="white-text">Autoquote</label>
          </div>
          <div class="input-field col s6">
            <a href="#" class="waves-effect waves-light btn white-text" v-if="!market.active" v-on:click="marketSubscribe">Subscribe</a>
            <a href="#" class="waves-effect waves-light btn white-text" v-if="market.active" v-on:click="marketUnSubscribe">Unsubscribe</a>
          </div>
          <table class="white-text">
           <thead>
             <tr>
                 <th data-field="product">Time</th>
                 <th data-field="price">BID</th>
                 <th data-field="party">ASK</th>
             </tr>
           </thead>
           <tbody>
             <row v-for="price in filteredPrices"
              :time="price.time"
              :bid="price.BID"
              :ask="price.ASK"
              >
            </row>
           </tbody>
         </table>
        </div>

        <div class="col s6">
          <h5 class="white-text">Message history</h5>
          <table class="white-text">
           <thead>
             <tr>
                 <th data-field="time">Time</th>
                 <th data-field="requestParty">Request party</th>
                 <th data-field="product">Product</th>
                 <th data-field="qty">Quantity</th>
             </tr>
           </thead>
           <tbody>
             <tr v-for="message in messages">
               <td>
                 {{message.date | moment}}
               </td>
               <td>
                 {{message.requestParty}}
               </td>
               <td>
                 {{message.product}}
               </td>
               <td>
                 {{message.quantity}}
               </td>
             </tr>
           </tbody>
         </table>
        </div>
      </div>
    </div>
    <vue-toastr ref="toastr"></vue-toastr>
  </div>
</template>

<script>
/* global Glue, glue, moment, alert */
import Row from './components/Row'
import Vue from 'vue'
import Toastr from 'vue-toastr'
require('vue-toastr/dist/vue-toastr.css')

Vue.component('vue-toastr', Toastr)

Vue.filter('moment', function (value) {
  if (!value) return ''
  return moment(value).format('h:mm:ss a')
})
export default {
  components: {
    Row
  },
  data () {
    return {
      messages: [],
      market: {
        symbol: 'EUR-USD',
        autoquote: true,
        spread: 0.1,
        active: false,
        prices: [],
        lastQuoteTime: new Date().getTime(),
        stream: {}
      },
      rfq: {
        stream: null,
        product: '',
        request: {}
      },
      counterParty: 'lspiro@tick42.com',
      parties: ['stoyan.damov@tick42.com', 'lspiro@tick42.com', 't42demo@tick42.com']
    }
  },
  computed: {
    filteredPrices: function () {
      var prices = this.market.prices || []
      return prices.slice(0, 3)
    }
  },
  watch: {
    'market.prices': function (val, oldVal) {
      if (val.length > 0) {
        if (this.market.autoquote && this.market.symbol === this.rfq.request.productName) {
          this.sendQuote()
        }
      }
    }
  },
  methods: {
    handleMarketData (data) {
      var prices = data.image || data.update
      if (prices) {
        prices.time = new Date()
        this.market.prices.unshift(prices)
      }
    },
    sendComment (message) {
      glue.agm.invoke('T42.RFQ.SendCommentToRequestParty', message, 'all', {}, function () {}, function (err) {
        console.log(err)
      })
    },
    getPrice () {
      if (this.rfq.request.quantity >= 0) {
        if (!this.market.prices[0].ASK) {
          return null
        }
        return Number(this.market.prices[0].ASK) + Number(this.market.spread)
      } else {
        if (!this.market.prices[0].BID) {
          return null
        }
        return Number(this.market.prices[0].BID) - Number(this.market.spread)
      }
    },
    sendQuote () {
      var self = this
      var price = this.getPrice()
      if (price !== null && this.market.autoquote) {
        if (!self.rfq.request.lastQuote || Math.abs(self.rfq.request.lastQuote - price) >= 0.00009) {
          if (new Date().getTime() - this.market.lastQuoteTime >= 5000) {
            this.market.lastQuoteTime = new Date().getTime()
            self.rfq.request.lastQuote = price
            glue.agm.invoke('T42.RFQ.SendQuoteResponse', {
              requestId: self.rfq.request.requestId,
              requestParty: self.rfq.request.requestParty,
              counterParty: self.counterParty,
              price: Number(price.toFixed(4))
            }, 'all', {}, function (s) {
              console.log(s)
            }, function (err) {
              console.log(err)
            })
          }
        }
      }
    },
    addMessage (message) {
      this.messages.unshift({
        date: new Date(message.requestExpirationDate), // message.requestExpirationDate,
        requestParty: message.requestParty,
        product: message.productName,
        quantity: parseInt(message.quantity)
      })
    },
    rfqRequest (data) {
      this.addMessage(data)
      this.rfq.request = data
      if (!this.market.autoquote || !this.market.active) {
        this.sendComment({
          requestParty: data.requestParty,
          counterParty: data.counterParty,
          comment: 'Not quoting at the moment'
        })
      } else if (this.market.symbol !== data.productName) {
        this.sendComment({
          requestParty: data.requestParty,
          counterParty: data.counterParty,
          comment: 'Not quoting for product ' + data.productName + 'at the moment'
        })
      } else {
        this.sendQuote()
      }
    },
    subscribeForRfq () {
      var self = this
      console.log(this.counterParty)
      if (this.rfq.stream) {
        this.rfq.stream.close()
      }

      glue.agm.subscribe('T42.RFQ.QuoteRequestStream', {
        arguments: {
          counterParty: self.counterParty
        },
        waitTimeoutMs: 10000
      }).then(function (stream) {
        self.rfq.stream = stream
        stream.onData(function (data) {
          self.rfqRequest(data.data)
        })
      }).catch((err) => {
        console.log(err)
        this.$refs.toastr.e('Couldn\'t subscribe for Quote Request Stream', 'Bummer!')
        self.rfq.stream = null
      })
    },
    marketSubscribe () {
      var self = this
      var symbol = ''
      if (this.market.symbol === 'EUR-USD') {
        symbol = 'EUR='
      } else if (this.market.symbol === 'EUR-GBP') {
        symbol = 'GBP='
      } else {
        alert('Unsupported product')
      }

      glue.agm.subscribe('T42.MarketStream.Subscribe', {
        arguments: {
          'Symbol': symbol,
          'Fields': 'BID,ASK'
        },
        waitTimeoutMs: 10000
      }).then(function (stream) {
        self.market.active = true
        self.market.stream = stream
        stream.onData(function (streamData) {
          self.handleMarketData(JSON.parse(streamData.data.data)[0])
        })
      }).catch((error) => {
        this.$refs.toastr.e('Couldn\'t subscribe for Market Stream', 'Bummer!')
        self.market.active = false
        self.market.stream = null
        console.log(error)
      })
    },

    marketUnSubscribe () {
      var stream = this.market.stream
      if (stream && stream.close) {
        stream.close()
      }
      this.market.stream = {}
      this.market.prices = []
      this.market.active = false
    }
  },
  created () {
    var self = this
    Glue({
      gateway: {
        protocolVersion: 1,
        ws: 'ws://localhost:22037'
      },
      auth: {
        username: 'IPIDOV',
        password: '0885366866'
      }
    }).then(function (glue) {
      window.glue = glue
      self.subscribeForRfq()
    })
  }
}
</script>
