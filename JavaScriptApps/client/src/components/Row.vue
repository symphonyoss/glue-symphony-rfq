<template>
  <tr>
    <td>{{product}}</td>
    <td>{{bestQuote}}</td>
    <td>{{bestQuoteParty}}</td>
    <td>{{bestTime | moment}}</td>
    <td>
      <div class="input-field">
        <select v-model="action" class="browser-default">
          <option value="+">Buy</option>
          <option value="-">Sell</option>
        </select>
      </div>
    </td>
    <td>
       <div class="input-field">
         <input placeholder="Quantity" id="quantity" type="number" min="0" class="validate" v-model="qty">
       </div>
    </td>
    <td>
      <a class="waves-effect waves-light btn" v-on:click="createRFQ" v-if="!subscribed">RFQ</a>
      <a class="waves-effect waves-light btn red" v-on:click="cancel" v-if="subscribed">Cancel</a>
    </td>
  </tr>
</template>

<script>
/* global glue */
export default {
  data () {
    return {
      bestTime: void 0,
      action: '+',
      qty: 1,
      bestQuote: '',
      bestQuoteParty: '',
      subscribed: false,
      subscription: {}
    }
  },
  methods: {
    cancel () {
      this.subscription.close()
      this.subscribed = false
      this.subscription = {}
    },
    isBetterQuote (quote) {
      if (!this.bestQuote) {
        return true
      }
      if (this.action === '+' && quote < this.bestQuote) {
        return true
      }

      if (this.action === '-' && quote > this.bestQuote) {
        return true
      }

      return false
    },
    createRFQ () {
      var self = this
      var action, quantity

      if (self.subscribed) {
        return
      }

      if (self.action === '+') {
        action = 'BUY'
        quantity = self.qty
      } else {
        action = 'SELL'
        quantity = (self.qty) * -1
      }
      glue.agm.subscribe('T42.RFQ.QuoteInquiryStream', {
        arguments: {
          requestParty: self.requestParty,
          counterParties: self.counterParties,
          productName: self.product,
          quantity: quantity,
          requestExpirationDate: new Date(new Date().getTime() + (self.expiration * 60 * 1000))
        },
        waitTimeoutMs: 1000
      }).then(function (stream) {
        stream.onData(function (streamData) {
          var data = streamData.data
          if (data.responseType === 'SetRequestId') {
            self.ondata({
              from: self.requestParty,
              to: self.counterParties,
              date: new Date(),
              message: 'RFQ ' + data.requestId + ': ' + action + ' ' + self.qty + ' ' + self.product
            })
          } else if (data.responseType === 'Quote') {
            if (self.isBetterQuote(data.price)) {
              self.bestQuote = data.price
              self.bestQuoteParty = data.counterParty
              self.bestTime = new Date()
            }
            self.ondata({
              from: data.counterParty,
              to: self.requestParty,
              date: new Date(),
              message: 'QUOTE ' + data.requestId + ' (' + self.product + ') @ ' + data.price
            })
          }
        })
        self.subscription = stream
        self.subscribed = true
      }).catch((err) => {
        this.$parent.$refs.toastr.e('Couldn\'t subscribe for RFQ stream', 'Bummer!')
        console.log(err)
      })
    }
  },
  props: ['product', 'ondata', 'requestParty', 'counterParties', 'expiration', 'glue']
}
</script>

<!-- Add "scoped" attribute to limit CSS to this component only -->
<style scoped>
h1 {
  color: #42b983;
}
</style>
