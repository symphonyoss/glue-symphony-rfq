<template>
  <div class="container">
    <div class="card-panel blue darken-4">
      <div class="row">
        <div class="input-field col s4">
          <strong class="white-text">Request Party</strong>
          <input class="white-text" type="email" v-model="requestParty" @change="subscribeForCounterPartyCommentStream">
        </div>

        <div class="input-field col s4">
          <strong class="white-text">Counter party</strong>
          <input-tag :tags="counterParties"></input-tag>
        </div>
        <div class="input-field col s4">
          <strong class="white-text">Expiration (in minutes)</strong>
          <input placeholder="30" id="rfq_expiration" type="number" class="validate white-text" v-model="expiration">
        </div>
      </div>
    </div>
    <div class="row">
      <div class="col s12">
        <div class="card-panel">
          <table>
           <thead>
             <tr>
                 <th data-field="product">Product Name</th>
                 <th data-field="price">Best Price</th>
                 <th data-field="party">Best Party</th>
                 <th data-field="party">Best Time</th>
                 <th data-field="action">Action</th>
                 <th data-field="quantity" style="width: 100px">Quantity</th>
                 <th data-field="RFQ">RFQ</th>
             </tr>
           </thead>
           <tbody>
             <row v-for="product in products"
              :product="product"
              :requestParty="requestParty"
              :counterParties="counterParties"
              :expiration="expiration"
              :glue="glue"
              :ondata="newMessage"
              >
            </row>
           </tbody>
         </table>
        </div>
      </div>
    </div>
    <div class="row">
      <div class="col s4">
        <div class="card-panel blue darken-4">
          <h4 class="white-text">Write message</h4>
          <div class="input-field">
            <input class="white-text" type="email" v-model="message.counterParty">
          </div>
          <div class="input-field">
            <textarea class="materialize-textarea white-text" v-model="message.content"></textarea>
            <label>Message</label>
          </div>
          <div class="input-field s2">
            <a class="waves-effect waves-light btn" v-on:click="sendMessage">SEND</a>
          </div>
        </div>
      </div>
      <div class="col s8">
        <h5>Message history</h5>
        <table>
         <thead>
           <tr>
               <th data-field="time">Time</th>
               <th data-field="from">From</th>
               <th data-field="to">To</th>
               <th data-field="message">Message</th>
           </tr>
         </thead>
         <tbody>
           <tr v-for="message in messages">
             <td>
               {{message.date | moment}}
             </td>
             <td>
               {{message.from}}
             </td>
             <td>
               {{message.to}}
             </td>
             <td>
               {{message.message}}
             </td>
           </tr>
         </tbody>
       </table>
      </div>
    </div>
    <vue-toastr ref="toastr"></vue-toastr>
  </div>
</template>

<script>
/* global $, Glue, glue, moment */
import Row from './components/Row'
import Vue from 'vue'
import InputTag from 'vue-input-tag'
import Toastr from 'vue-toastr'

Vue.component('vue-toastr', Toastr)

require('vue-toastr/dist/vue-toastr.css')

Vue.filter('moment', function (value) {
  if (!value) return ''
  return moment(value).format('h:mm:ss a')
})
export default {
  components: {
    Row, InputTag
  },
  methods: {
    sendMessage () {
      var message = {
        requestParty: this.requestParty,
        counterParty: this.message.counterParty,
        comment: this.message.content
      }
      this.newMessage({
        date: new Date(),
        from: message.requestParty,
        to: message.counterParty,
        message: message.comment
      })
      glue.agm.invoke('T42.RFQ.SendCommentToCounterParty', message)
    },
    newMessage (message) {
      this.messages.unshift(message)
    },
    subscribeForCounterPartyCommentStream (e, n) {
      var self = this
      glue.agm.subscribe('T42.RFQ.CounterPartyCommentStream', {
        arguments: {
          requestParty: self.requestParty
        },
        waitTimeoutMs: 10000
      }).then(function (stream) {
        self.counterPartyCommentStream[self.requestParty] = stream
        stream.onData(function (streamData) {
          var data = streamData.data
          self.newMessage({
            date: new Date(),
            from: data.counterParty,
            to: data.requestParty,
            message: data.comment
          })
        })
      }).catch((err) => {
        console.log(err)
        this.$refs.toastr.e('Couldn\'t subscribe for Comment stream', 'Bummer!')
      })
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
        password: '1'
      }
    }).then(function (glue) {
      window.glue = glue
      self.subscribeForCounterPartyCommentStream()
    })

    $(document).ready(function () {
      $('select').material_select()
    })
  },
  data () {
    return {
      commentStreams: {},
      counterPartyCommentStream: {},
      glue: {},
      products: ['EUR-USD', 'EUR-GBP'],
      parties: ['stoyan.damov@tick42.com', 'lspiro@tick42.com', 't42demo@tick42.com'],
      expiration: 15,
      counterParties: ['stoyan.damov@tick42.com', 'lspiro@tick42.com'],
      requestParty: 't42demo@tick42.com',
      messages: [],
      message: {
        counterParty: 't42demo@tick42.com',
        content: ''
      }
    }
  }
}
</script>

<style media="screen">
  .vue-input-tag-wrapper input[type=text]:focus:not([readonly]) {
    box-shadow: none;
    border-bottom: none;
  }
  .vue-input-tag-wrapper .new-tag {
    margin: 0 !important;
    padding: 0 !important;
  }
  .vue-input-tag-wrapper {
    padding-top: 0 !important;
    border: none !important;
    border-radius: 2px !important;
    background-color: rgba(255, 255, 255, 0.9) !important;
  }
  input[type=email], input[type=number] {
    margin-bottom: 0 !important;
  }
</style>
